using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ExcelDataReader;
using Reactive.Bindings;
using uic_addin.Models;
using System.Text;

namespace uic_addin.Views; 
internal class NaicsFinderPaneViewModel : ViewStatePane {
    private const string ViewPaneId = "NaicsFinderPane";
    private readonly string _allNaicsPath;
    private readonly string _twoDigitCodesPath;

    /// <summary>
    ///     Consume the passed in CIMView. Call the base constructor to wire up the CIMView.
    /// </summary>
    public NaicsFinderPaneViewModel(CIMView view)
        : base(view) {
        var documentsFolder = Path.Combine(AddinAssemblyLocation(), "NaicsDocuments");
        _twoDigitCodesPath = Path.Combine(documentsFolder, "2_6_digit_2017_Codes.xlsx");
        _allNaicsPath = Path.Combine(documentsFolder, "2017_NAICS_Index_File.xlsx");

        FilteredNaics = new ReadOnlyObservableCollection<NaicsModel>(NaicsModels);

        var categories = new List<KeyValuePair<object, string>>(26) {
            new(11, "Agriculture, Forestry, Fishing and Hunting"),
            new(21, "Mining, Quarrying, and Oil and Gas Extraction"),
            new(22, "Utilities"),
            new(23, "Construction"),
            new("31-33", "Manufacturing"),
            new(42, "Wholesale Trade"),
            new("44-45", "Retail Trade"),
            new("48-49", "Transportation and Warehousing"),
            new(51, "Information"),
            new(52, "Finance and Insurance"),
            new(53, "Real Estate and Rental and Leasing"),
            new(54, "Professional, Scientific, and Technical Services"),
            new(55, "Management of Companies and Enterprises"),
            new(56,
                "Administrative and Support and Waste Management and Remediation Services"),
            new(61, "Educational Services"),
            new(62, "Health Care and Social Assistance"),
            new(71, "Arts, Entertainment, and Recreation"),
            new(72, "Accommodation and Food Services"),
            new(81, "Other Services (except Public Administration)"),
            new(92, "Public Administration")
        };

        NaicsCategories = new ObservableCollection<KeyValuePair<object, string>>(categories);
    }

    public ReactiveProperty<string> InputCode { get; set; } = new ReactiveProperty<string>();

    public ReactiveProperty<int> CurrentCode { get; set; } = new ReactiveProperty<int>();

    public ReactiveProperty<bool> Expanded { get; set; }

    public RelayCommand ShowCategory { get; set; }

    public ObservableCollection<KeyValuePair<object, string>> NaicsCategories { get; set; }

    private ObservableCollection<NaicsModel> NaicsModels { get; } = [];

    public ReadOnlyObservableCollection<NaicsModel> FilteredNaics { get; set; }

    public List<NaicsModel> NaicsCodes { get; set; } = [];

    /// <summary>
    ///     Must be overridden in child classes used to persist the state of the view to the CIM.
    /// </summary>
    public override CIMView ViewState {
        get {
            _cimView.InstanceID = (int)InstanceID;
            return _cimView;
        }
    }

    public List<NaicsModel> AllNaicsCodes { get; set; } = [];

    public ReactiveProperty<string> UpdateSearch { get; set; }

    public ReactiveProperty<bool> HasValue { get; set; }

    public ReactiveCommand SetClipboard { get; set; }

    /// <summary>
    ///     Create a new instance of the pane.
    /// </summary>
    internal static NaicsFinderPaneViewModel Create() {
        var view = new CIMGenericView {
            ViewType = ViewPaneId
        };

        return FrameworkApplication.Panes.Create(ViewPaneId, view) as NaicsFinderPaneViewModel;
    }

    /// <summary>
    ///     Called when the pane is initialized.
    /// </summary>
    protected override async Task InitializeAsync() {
        await base.InitializeAsync();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        ShowCategory = new RelayCommand(SetActive, () => true);
        HasValue = CurrentCode.Select(x => x.ToString().Length == 6).ToReactiveProperty();
        SetClipboard = HasValue.ToReactiveCommand()
                               .WithSubscribe(() => Clipboard.SetText(CurrentCode.Value.ToString()));
        Expanded = CurrentCode.Select(x => x < 10).ToReactiveProperty();
        UpdateSearch = InputCode.Select(x => x)
                                .Throttle(TimeSpan.FromMilliseconds(200))
                                .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current))
                                .Do(async x => await FilterNaicsByString(x))
                                .ToReactiveProperty();
        SetInputCode = new RelayCommand(x => { InputCode.Value = x.ToString(); }, () => true);

        var codeColumn = 1;
        var titleColumn = 2;
        var skipRows = 2;
        var currentRow = 0;

        try {
            using var stream = File.Open(_twoDigitCodesPath, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            do {
                while (reader.Read()) {
                    currentRow += 1;
                    if (currentRow <= skipRows) {
                        continue;
                    }

                    try {
                        NaicsCodes.Add(new NaicsModel(reader.GetDouble(codeColumn),
                                                      reader.GetString(titleColumn)));
                    } catch (InvalidCastException) {
                        NaicsCodes.AddRange(NaicsModel.CreateNaicsFromRange(reader.GetString(codeColumn),
                                                                            reader.GetString(titleColumn)));
                    }
                }
            } while (reader.NextResult());
        } catch (Exception) {
        }

        currentRow = 0;
        skipRows = 1;
        codeColumn = 0;
        titleColumn = 1;

        try {
            using var stream = File.Open(_allNaicsPath, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            do {
                while (reader.Read()) {
                    currentRow += 1;
                    if (currentRow <= skipRows) {
                        continue;
                    }

                    AllNaicsCodes.Add(new NaicsModel(reader.GetDouble(codeColumn),
                                                     reader.GetString(titleColumn)));
                }
            } while (reader.NextResult());
        } catch (Exception) {
        }
    }

    public RelayCommand SetInputCode { get; set; }

    private async Task FilterNaicsByString(string input) => await QueuedTask.Run(() => {
        if (string.IsNullOrEmpty(input) || input.Length < 3) {
            Application.Current.Dispatcher.Invoke(NaicsModels.Clear);

            return;
        }

        IEnumerable<NaicsModel> codes;

        if (int.TryParse(input, out var _)) {
            codes = AllNaicsCodes.Where(x => x.Code.ToString().StartsWith(input));
        } else {
            codes = AllNaicsCodes.Where(x => x.Title.ToLower().Contains(input.ToLower()));
        }

        Application.Current.Dispatcher.Invoke(NaicsModels.Clear);

        foreach (var model in codes) {
            Application.Current.Dispatcher.Invoke(() => NaicsModels.Add(model));
        }
    });

    /// <summary>
    ///     Called when the pane is uninitialized.
    /// </summary>
    protected override async Task UninitializeAsync() => await base.UninitializeAsync();

    public void SetActive(object item) {
        if (item == null || !int.TryParse(item.ToString(), out var code)) {
            return;
        }

        CurrentCode.Value = code;

        var start = code * 10;

        var depth = code.ToString().Length;
        var end = Convert.ToInt32(Math.Pow(10, depth)) - code + start;

        IEnumerable<NaicsModel> codes;
        if (depth == 6) {
            codes = AllNaicsCodes.Where(x => x.Code == code);
        } else {
            codes = NaicsCodes.Where(x => x.Code >= start && x.Code <= end);
        }

        NaicsModels.Clear();

        foreach (var model in codes) {
            NaicsModels.Add(model);
        }
    }

    private static string AddinAssemblyLocation() {
        var asm = Assembly.GetExecutingAssembly();

        return Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(asm.Location).LocalPath));
    }
}
