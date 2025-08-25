using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ArcGIS.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ProEvergreen;
using Reactive.Bindings;
using Serilog;
using Serilog.Events;
using uic_addin.Models;
using Module = ArcGIS.Desktop.Framework.Contracts.Module;

namespace uic_addin; 
internal class UicModule : Module {
    private static UicModule _this;
    private readonly IEnumerable<string> _addinKeys = ["UICAddin.Evergreen.BetaChannel"];
    public readonly Dictionary<string, DockPane> DockPanes = new(2);
    public readonly string HasUpdateState = "has_update";
    public readonly string HasImplementationState = "has_implemented";
    public readonly Dictionary<string, Layer> Layers = new(1);
    private SubscriptionToken _token;

    public ReactiveProperty<Evergreen> Evergreen { get; } = new ReactiveProperty<Evergreen> {
        Value = new Evergreen("agrc", "uic-addin")
    };

    public ReactiveProperty<bool> IsCurrent { get; } = new ReactiveProperty<bool>(true);

    public EvergreenSettings EvergreenSettings { get; set; } = new EvergreenSettings {
        BetaChannel = true
    };

    public static UicModule Current => _this ??= (UicModule)FrameworkApplication.FindModule("UICModule");

    public Dictionary<string, string> Settings { get; set; } = [];

    protected override bool Initialize() {
        SetupLogging();

        FrameworkApplication.State.Deactivate(HasImplementationState);

        _ = QueuedTask.Run(async () => {
            await CheckForLastest();

            Log.Debug("Initializing UIC Workflow Addin {version}", EvergreenSettings.CurrentVersion.AddInVersion);
        });

        if (MapView.Active == null) {
            _token = MapViewInitializedEvent.Subscribe(args => CacheLayers(args.MapView));
        } else {
            CacheLayers(MapView.Active);

            if (Layers.Count < 1 || Layers.Any(x => x.Value == null)) {
                return false;
            }
        }

        return true;
    }

    private void SetupLogging() {
        var addinFolder = GetAddinFolder();
        var logLocation = Path.Combine(addinFolder, "{Date}-log.txt");

        // Try to read API key from environment variable or configuration
        var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY") ?? "your-api-key-here";

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Email(options: new() {
                From = "noreply@utah.gov",
                To = ["ugrc-developers@utah.gov"],
                Host = "smtp.sendrid.net",
                Credentials = new NetworkCredential("apiKey", apiKey)
            },
             restrictedToMinimumLevel: LogEventLevel.Error)
            .WriteTo.File(logLocation, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .MinimumLevel.Verbose()
            .CreateLogger();
    }

    public string GetAddinFolder() {
        var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var arcGisProLocation = Path.Combine(myDocs, "ArcGIS", "AddIns", "ArcGISPro");

        var attribute = (GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute),
                                                                                           true)[0];
        var proAddinFolder = $"{{{attribute.Value}}}";

        var addinFolder = Path.Combine(arcGisProLocation, proAddinFolder);

        return addinFolder;
    }

    protected override async Task OnReadSettingsAsync(ModuleSettingsReader settings) {
        Settings.Clear();

        if (settings == null) {
            try {
                await CheckForLastest();
            } catch {
                // ignored
            }

            await base.OnReadSettingsAsync(null);

            return;
        }

        foreach (var key in _addinKeys) {
            var value = settings.Get(key);

            if (value != null) {
                Settings[key] = value.ToString();
            }
        }

        EvergreenSettings.BetaChannel = Convert.ToBoolean(Settings["UICAddin.Evergreen.BetaChannel"]);

        try {
            await CheckForLastest();
        } catch {
            // ignored
        }
    }

    protected override async Task OnWriteSettingsAsync(ModuleSettingsWriter settings) {
        foreach (var key in Settings.Keys) {
            settings.Add(key, Settings[key]);
        }

        try {
            await CheckForLastest();
        } catch {
            // ignored
        }
    }

    protected override bool CanUnload() {
        foreach (var pane in DockPanes.Values) {
            if (pane.IsVisible) {
                pane.Hide();
            }
        }

        return true;
    }

    public void CacheLayers(MapView view = null) {
        Log.Debug("Caching project layers");

        if (_token != null) {
            MapViewInitializedEvent.Unsubscribe(_token);
        }

        var activeView = MapView.Active ?? view;

        if (activeView == null) {
            Log.Debug("MapView is empty");

            return;
        }

        // Layers[FacilityModel.TableName] = LayerService.GetTable(FacilityModel.TableName, activeView.Map)
        //                                    as FeatureLayer;
    }

    private void FindPaneFromId(string id) {
        if (DockPanes.ContainsKey(id)) {
            return;
        }

        DockPanes[id] = FrameworkApplication.DockPaneManager.Find(id);
    }

    public async Task CheckForLastest() {
        var useBetaChannel = true;
        if (Current.Settings.TryGetValue("UICAddin.Evergreen.BetaChannel", out var value)) {
            _ = bool.TryParse(value, out useBetaChannel);
        }

        EvergreenSettings.LatestRelease = await Evergreen.Value.GetLatestReleaseFromGithub(useBetaChannel);
        EvergreenSettings.CurrentVersion = Evergreen.Value.GetCurrentAddInVersion();

        try {
            IsCurrent.Value = Evergreen.Value.IsCurrent(EvergreenSettings.CurrentVersion.AddInVersion,
                                                        EvergreenSettings.LatestRelease);
        } catch (ArgumentNullException) {
            if (EvergreenSettings.CurrentVersion == null) {
                // pro addin version couldnt be found
                throw;
            }

            // github doesn't have a version. most likely only prereleases and no stable
            IsCurrent.Value = true;
        }

        var wrapper = FrameworkApplication.GetPlugInWrapper("UpdateAvailableButton");

        if (IsCurrent.Value) {
            FrameworkApplication.State.Deactivate(HasUpdateState);
            wrapper.Caption = "Up to date! 💙";
            wrapper.DisabledTooltip = $"You are using the most recent version: {Current.EvergreenSettings?.LatestRelease?.TagName}!";
            wrapper.TooltipHeading = "UIC Add-in";

        } else {
            FrameworkApplication.State.Activate(HasUpdateState);
            wrapper.Caption = "Update Available";
            wrapper.TooltipHeading = "Stay Current!";
            wrapper.Tooltip = $"Update to {Current.EvergreenSettings?.LatestRelease?.TagName}";
        }
    }
}
