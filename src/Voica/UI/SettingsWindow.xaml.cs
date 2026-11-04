using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Voica.UI;

/// <summary>
/// Settings window (spec §3–§11). All settings apply immediately; the Groq API key applies only on
/// Save (spec §9). Also hosts "Delete all data" with random-phrase confirmation (spec §11).
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly Action _onHotkeyChanged;
    private bool _loaded;

    public double SettingsContentMaxHeight { get; } = Math.Max(180, Math.Min(640, SystemParameters.WorkArea.Height - 200));
    public double SettingsContentMinHeight => Math.Min(420, SettingsContentMaxHeight);
    public int SectionCount => Tabs.Items.Count;

    public SettingsWindow(Action onHotkeyChanged)
    {
        _onHotkeyChanged = onHotkeyChanged;
        InitializeComponent();
        Width = Math.Min(900, SystemParameters.WorkArea.Width - 16);
        MaxHeight = SystemParameters.WorkArea.Height;
        LoadFromPrefs();
        RefreshProviderKeys();
        Closed += (_, _) => _downloadCts?.Cancel();
    }

    private void OnHeaderMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        // Tabs and buttons in the shared title bar keep their normal mouse behaviour.
        for (var hit = e.OriginalSource as DependencyObject; hit is not null && hit != sender;
             hit = System.Windows.Media.VisualTreeHelper.GetParent(hit))
            if (hit is System.Windows.Controls.Primitives.ButtonBase or TabItem) return;
        DragMove();
    }

    private void OnHelpClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ToolTip: ToolTip tip } button)
        {
            tip.PlacementTarget = button;
            tip.StaysOpen = false;
            tip.IsOpen = !tip.IsOpen;
        }
    }

    private void OnHelpLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ToolTip: ToolTip tip }) tip.IsOpen = false;
    }

    private System.Windows.Media.Brush StatusBrush(string key) =>
        (System.Windows.Media.Brush)FindResource(key);

    public void RefreshPersistentOverlay()
    {
        bool loaded = _loaded;
        _loaded = false;
        PersistentOverlayCheck.IsChecked = Prefs.PersistentOverlay;
        _loaded = loaded;
    }

    private void OnPersistentOverlayChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.PersistentOverlay = PersistentOverlayCheck.IsChecked == true;
        _onHotkeyChanged();
    }

    private void OnLivePreviewChanged(object sender, RoutedEventArgs e)
    {
        if (_loaded) Prefs.LivePreview = LivePreviewCheck.IsChecked == true;
    }

    private void OnPipelineOrderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loaded && PipelineOrderCombo.SelectedIndex >= 0) Prefs.CleanupBeforeTranslation = PipelineOrderCombo.SelectedIndex == 0;
    }

    private void OnPipelineToggle(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.TranslationEnabled = TranslateCheck.IsChecked == true;
        Prefs.CleanupEnabled = CleanupCheck.IsChecked == true;
        RefreshPipelineSettings();
        RefreshSetupHints();
    }

    private void RefreshPipelineSettings() => PipelineOrderPanel.IsEnabled =
        TranslateCheck.IsChecked == true && CleanupCheck.IsChecked == true;
    private void OnPresetFix(object sender, RoutedEventArgs e) => CleanupPromptBox.Text = CleanupPrompts.Default;
    private void OnPresetShort(object sender, RoutedEventArgs e) => CleanupPromptBox.Text = CleanupPrompts.Concise;
    private void OnPresetFormal(object sender, RoutedEventArgs e) => CleanupPromptBox.Text = CleanupPrompts.Formal;
    private void OnSavePipeline(object sender, RoutedEventArgs e)
    {
        Prefs.TranslationModel = TranslationModelBox.Text.Trim();
        Prefs.CleanupModel = CleanupModelBox.Text.Trim();
        Prefs.CleanupPrompt = CleanupPromptBox.Text.Trim();
        PipelineStatus.Text = Loc.T("Model and instruction saved.", "Модель и промпт сохранены.");
        RefreshSetupHints();
    }

    private void OnProvidersChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || TranslationProviderCombo.SelectedIndex < 0 || CleanupProviderCombo.SelectedIndex < 0) return;
        string translation = Providers.Ids[TranslationProviderCombo.SelectedIndex];
        string cleanup = Providers.Ids[CleanupProviderCombo.SelectedIndex];
        if (translation != Prefs.TranslationProvider) { Prefs.TranslationProvider = translation; TranslationKeyBox.Clear(); }
        if (cleanup != Prefs.CleanupProvider) { Prefs.CleanupProvider = cleanup; CleanupKeyBox.Clear(); }
        RefreshProviderKeys();
    }
    private void OnProviderKeyChanged(object sender, RoutedEventArgs e)
    {
        if (_loaded) RefreshProviderKeys();
    }
    private void RefreshProviderKeys()
    {
        bool translationSaved = !string.IsNullOrEmpty(TranslatorKeyStore.Load("translation", Prefs.TranslationProvider));
        bool cleanupSaved = !string.IsNullOrEmpty(TranslatorKeyStore.Load("cleanup", Prefs.CleanupProvider));
        TranslationKeyStatus.Text = translationSaved ? Loc.T("Translation key saved", "Ключ перевода сохранён") : Loc.T("No translation key", "Ключ перевода не задан");
        CleanupKeyStatus.Text = cleanupSaved ? Loc.T("Editing key saved", "Ключ обработки сохранён") : Loc.T("No editing key", "Ключ обработки не задан");
        TranslationKeyMask.Visibility = translationSaved && TranslationKeyBox.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        CleanupKeyMask.Visibility = cleanupSaved && CleanupKeyBox.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshSetupHints();
    }

    private void RefreshSetupHints()
    {
        var status = SetupStatus.Read();
        RecognitionSetupHint.Text = SetupStatus.Message(status.RecognitionIssue);
        RecognitionSetupHint.Visibility = status.RecognitionIssue == SetupIssue.None ? Visibility.Collapsed : Visibility.Visible;
        PipelineSetupHint.Text = SetupStatus.Message(status.PipelineIssue);
        PipelineSetupHint.Visibility = status.PipelineIssue == SetupIssue.None ? Visibility.Collapsed : Visibility.Visible;
    }
    private void ChangeKey(string stage, bool save)
    {
        var box = stage == "translation" ? TranslationKeyBox : CleanupKeyBox;
        var provider = stage == "translation" ? Prefs.TranslationProvider : Prefs.CleanupProvider;
        try
        {
            if (save && string.IsNullOrWhiteSpace(box.Password)) { MessageBox.Show(this, Loc.T("Enter an API key first.", "Сначала введите API-ключ.")); return; }
            if (save) TranslatorKeyStore.Save(stage, provider, box.Password); else TranslatorKeyStore.Delete(stage, provider);
            box.Clear(); RefreshProviderKeys();
        }
        catch (Exception ex) { MessageBox.Show(this, Loc.T("Could not update the key: ", "Не удалось изменить ключ: ") + ex.Message); }
    }
    private void OnSaveTranslationKey(object sender, RoutedEventArgs e) => ChangeKey("translation", true);
    private void OnDeleteTranslationKey(object sender, RoutedEventArgs e) => ChangeKey("translation", false);
    private void OnSaveCleanupKey(object sender, RoutedEventArgs e) => ChangeKey("cleanup", true);
    private void OnDeleteCleanupKey(object sender, RoutedEventArgs e) => ChangeKey("cleanup", false);

    private void LoadFromPrefs()
    {
        _loaded = false;
        TranslationProviderCombo.ItemsSource = new[] { "OpenRouter", "RouterAI", "Царь Роутер" };
        CleanupProviderCombo.ItemsSource = new[] { "OpenRouter", "RouterAI", "Царь Роутер" };
        TranslationProviderCombo.SelectedIndex = Array.IndexOf(Providers.Ids, Prefs.TranslationProvider);
        CleanupProviderCombo.SelectedIndex = Array.IndexOf(Providers.Ids, Prefs.CleanupProvider);
        TranslationModelBox.Text = Prefs.TranslationModel;
        PipelineOrderCombo.ItemsSource = new[] { Loc.T("Edit, then translate", "Сначала обработка, потом перевод"), Loc.T("Translate, then edit", "Сначала перевод, потом обработка") };
        PipelineOrderCombo.SelectedIndex = Prefs.CleanupBeforeTranslation ? 0 : 1;
        TranslateCheck.IsChecked = Prefs.TranslationEnabled;
        CleanupCheck.IsChecked = Prefs.CleanupEnabled;
        RefreshPipelineSettings();
        CleanupModelBox.Text = Prefs.CleanupModel;
        CleanupPromptBox.Text = Prefs.CleanupPrompt;

        EngineCombo.ItemsSource = new[] { S.EngineCloud, S.EngineLocal };
        EngineCombo.SelectedIndex = Prefs.Engine == EngineKind.Local ? 1 : 0;
        RefreshModelStatus();

        ModeCombo.ItemsSource = new[] { S.ModePtt, S.ModeToggle };
        ModeCombo.SelectedIndex = Prefs.Mode == DictationMode.Ptt ? 0 : 1;

        KeyCombo.ItemsSource = HotkeyBinding.Presets.Select(p => p.DisplayName()).ToList();
        RefreshHotkeyUi();

        OutputCombo.ItemsSource = new[] { S.OutputInsert, S.OutputWindow };
        OutputCombo.SelectedIndex = Prefs.Output == OutputMode.Insert ? 0 : 1;
        KeepClipboardCheck.IsChecked = Prefs.KeepTextInClipboard;

        // Cloud STT model / language (spec §2); order matches GroqClient's arrays.
        DoubleTapCheck.IsChecked = Prefs.DoubleTapToStart;
        OverlayCheck.IsChecked = Prefs.ShowOverlay;
        LivePreviewCheck.IsChecked = Prefs.LivePreview;
        PersistentOverlayCheck.IsChecked = Prefs.PersistentOverlay;
        SttModelCombo.ItemsSource = new[] { S.SttTurbo, S.SttLarge };
        SttModelCombo.SelectedIndex = Array.IndexOf(GroqClient.SttModels, Prefs.SttModel);
        LanguageCombo.ItemsSource = new[] { S.LangAuto, S.LangRu, S.LangEn };
        LanguageCombo.SelectedIndex = Array.IndexOf(GroqClient.Languages, Prefs.Language);
        RefreshCloudSettings();

        StoreAudioCheck.IsChecked = Prefs.StoreAudio;
        SystemProxyCheck.IsChecked = Prefs.UseSystemProxy;
        RefreshProxyRoute();
        NotifyInsertCheck.IsChecked = Prefs.NotifyOnInsert;
        CheckUpdatesCheck.IsChecked = Prefs.CheckUpdatesOnLaunch;
        AboutVersionText.Text = string.Format(S.AboutVersionFmt, AppInfo.Version);
        RetentionBox.Text = Prefs.RetentionDays.ToString();
        VocabularyBox.Text = Prefs.Vocabulary;
        UpdateVocabCounter();
        // Rules that change words, each with its own switch (spec §6.2/§6.3/§6.4).
        FillersCheck.IsChecked = Prefs.RemoveFillers;
        QuotesCheck.IsChecked = Prefs.FixQuotes;
        TermRulesCheck.IsChecked = Prefs.FixTermsByRules;
        LlmCheck.IsChecked = Prefs.LlmPostProcess;
        RefreshCloudSettings();
        LlmStatusText.Text = "";
        ChatModelPanel.Visibility = Prefs.LlmPostProcess ? Visibility.Visible : Visibility.Collapsed;
        ChatModelCombo.ItemsSource = new[] { Prefs.ChatModel == ChatModels.Auto ? S.ChatModelAuto : Prefs.ChatModel };
        ChatModelCombo.SelectedIndex = 0;

        // Prefill the saved key (masked) so "Show" can reveal it, like the macOS app.
        KeyBox.Password = KeyStore.Load() ?? "";
        RefreshKeyStatus();
        _loaded = true;

        // Spec §6.1 UX: probe model availability when opening with the toggle already on.
        if (Prefs.LlmPostProcess)
            _ = ProbeChatModelAsync();
    }

    // --- Recognition engine + model (spec §2.5) ---

    private System.Threading.CancellationTokenSource? _downloadCts;

    private void OnEngineChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.Engine = EngineCombo.SelectedIndex == 1 ? EngineKind.Local : EngineKind.Cloud;
        RefreshCloudSettings();

        // Switching back to the cloud mid-download means the model is not wanted after all — no
        // reason to keep spending someone's traffic on it (macOS parity).
        if (Prefs.Engine == EngineKind.Cloud) CancelModelDownload();

        // Choosing the local engine must NOT start a download by itself (spec §9.5): behind a
        // proxy that wants credentials it is an instant 407 nobody asked for and nobody understands.
        // The Download button appears right here instead, and the download starts when it is
        // pressed.
        RefreshModelStatus();
        RefreshNoKeyHint();   // the way out of "no key" depends on which engine is selected (§11.3)
    }

    private void RefreshCloudSettings()
    {
        bool cloud = Prefs.Engine == EngineKind.Cloud;
        SttModelCombo.IsEnabled = cloud; LanguageCombo.IsEnabled = cloud;
        CloudRecognitionOptions.IsEnabled = cloud;
        CloudRecognitionOptions.Visibility = cloud ? Visibility.Visible : Visibility.Collapsed;
        CloudRecognitionPanel.Visibility = cloud || LlmCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        CloudInactiveHint.Visibility = cloud ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RefreshModelStatus()
    {
        bool installed = ModelManager.IsInstalled();
        bool downloading = _downloadCts is not null;
        long mb = ModelManager.TotalSize / (1024 * 1024);

        if (!downloading)
            ModelStatusText.Text = installed
                ? string.Format(S.ModelDownloadedFmt, mb)
                : string.Format(Loc.T("Model not installed ({0} MB)", "Модель не установлена ({0} МБ)"), mb);

        SelectLocalModelButton.Content = Loc.T("Choose existing model…", "Выбрать установленную модель…");
        SelectLocalModelButton.Visibility = !installed && !downloading ? Visibility.Visible : Visibility.Collapsed;
        ModelLocationText.Text = installed ? string.Format(Loc.T("Model folder: {0}", "Папка модели: {0}"), ModelManager.ModelsDir) : "";
        DownloadModelButton.Visibility = !installed && !downloading ? Visibility.Visible : Visibility.Collapsed;
        CancelDownloadButton.Visibility = downloading ? Visibility.Visible : Visibility.Collapsed;
        ModelProgress.Visibility = downloading ? Visibility.Visible : Visibility.Collapsed;

        // Data & privacy: on-disk footprint + delete.
        DeleteModelButton.IsEnabled = installed;
        ModelDiskText.Text = string.Format(S.ModelDiskFmt, installed ? mb : 0);
        RefreshSetupHints();
    }

    private void OnSelectLocalModel(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Loc.T("Select the folder with the GigaAM v3 ONNX files", "Выберите папку с файлами GigaAM v3 ONNX") };
        if (dialog.ShowDialog(this) == true) _ = ImportExistingModelAsync(dialog.FolderName);
    }

    public async System.Threading.Tasks.Task ImportExistingModelAsync(string folder)
    {
        if (_downloadCts is not null || ModelManager.IsInstalled()) return;
        _downloadCts = new System.Threading.CancellationTokenSource();
        RefreshModelStatus();
        ModelStatusText.Text = Loc.T("Checking model files…", "Проверка файлов модели…");
        try
        {
            var progress = new Progress<double>(p => { ModelProgress.Value = p * 100; ModelStatusText.Text = string.Format(Loc.T("Copying model… {0}%", "Копирование модели… {0}%"), (int)(p * 100)); });
            await ModelManager.ImportExistingAsync(folder, progress, _downloadCts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Error("local model import failed", ex);
            MessageBox.Show(this, ex.Message, Loc.T("Could not use model", "Не удалось использовать модель"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _downloadCts.Dispose();
            _downloadCts = null;
            RefreshModelStatus();
        }
    }

    private void OnDownloadModel(object sender, RoutedEventArgs e)
    {
        if (_downloadCts is null) _ = DownloadModelAsync();
    }

    /// <summary>Stops a download in progress (macOS parity): 215 MB is worth changing your mind about.</summary>
    private void OnCancelModelDownload(object sender, RoutedEventArgs e) => CancelModelDownload();

    private void CancelModelDownload()
    {
        if (_downloadCts is null) return;
        Log.Info("model download cancelled by the user");
        _downloadCts.Cancel();
    }

    private async System.Threading.Tasks.Task DownloadModelAsync()
    {
        _downloadCts = new System.Threading.CancellationTokenSource();
        RefreshModelStatus();
        try
        {
            var progress = new Progress<double>(p =>
            {
                ModelProgress.Value = p * 100;
                ModelStatusText.Text = string.Format(S.ModelDownloadingFmt, (int)(p * 100));
            });
            await ModelManager.DownloadAsync(progress, _downloadCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Asked for, not gone wrong: the status line goes back to "not downloaded" by itself
            // and nothing else has to be said. A dialog here would be the app arguing with a
            // button it offered.
        }
        catch (Exception ex)
        {
            Log.Error("model download failed", ex);
            // Through the shared translation (spec §9.5): behind a corporate proxy this is the most
            // common failure of all, and "407" tells the user nothing.
            MessageBox.Show(string.Format(S.ModelDownloadFailedFmt, Net.Describe(ex, ModelManager.ReleaseUri)), AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _downloadCts.Dispose();
            _downloadCts = null;
            RefreshModelStatus();
        }
    }

    private void OnDeleteModel(object sender, RoutedEventArgs e)
    {
        // Ask first. It is one click, and the way back is a 214 MB download — which in the networks
        // this was built for (§9.5) is exactly what people cannot easily repeat. macOS asks too.
        long mb = ModelManager.TotalSize / (1024 * 1024);
        var confirm = MessageBox.Show(this, string.Format(S.ModelDeleteAskFmt, mb), S.ModelDeleteTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        ModelManager.Delete();
        Log.Info("local model deleted from Settings");
        RefreshModelStatus();
    }

    // --- Immediate-apply settings ---

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.Mode = ModeCombo.SelectedIndex == 0 ? DictationMode.Ptt : DictationMode.Toggle;
        _onHotkeyChanged();
    }

    private void OnKeyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || KeyCombo.SelectedIndex < 0) return;
        Prefs.Hotkey = HotkeyBinding.Presets[KeyCombo.SelectedIndex];
        _onHotkeyChanged();
        RefreshHotkeyUi();
    }

    private void OnCustomKey(object sender, RoutedEventArgs e)
    {
        var dialog = new HotkeyCaptureDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is { } binding)
        {
            Prefs.Hotkey = binding;
            _onHotkeyChanged();
            RefreshHotkeyUi();
        }
    }

    /// <summary>Reflects the current binding: selects the matching preset (or none) and shows its name.</summary>
    private void RefreshHotkeyUi()
    {
        var wasLoaded = _loaded;
        _loaded = false;   // avoid re-entering OnKeyChanged while we set the selection
        var current = Prefs.Hotkey;
        int index = HotkeyBinding.Presets.ToList().FindIndex(p => p == current);
        KeyCombo.SelectedIndex = index;   // -1 for a custom combo
        CurrentHotkeyText.Text = string.Format(S.HotkeyCurrentFmt, current.DisplayName());
        // A bare Left Alt takes the system's Alt+Shift layout switch with it (spec §4) — the key
        // stays on offer, but not silently.
        HotkeyLayoutWarning.Visibility = LayoutSwitch.ShouldWarn(current)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _loaded = wasLoaded;
    }

    private void OnOutputChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.Output = OutputCombo.SelectedIndex == 0 ? OutputMode.Insert : OutputMode.Window;
    }

    private void OnKeepClipboardChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.KeepTextInClipboard = KeepClipboardCheck.IsChecked == true;
    }

    private void OnDoubleTapChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.DoubleTapToStart = DoubleTapCheck.IsChecked == true;
        _onHotkeyChanged();   // re-applies the hotkey settings live
    }

    private void OnOverlayChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.ShowOverlay = OverlayCheck.IsChecked == true;
        _onHotkeyChanged();   // the tray re-applies the indicator (capsule vs icon, spec §4.2)
    }

    private void OnSttModelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || SttModelCombo.SelectedIndex < 0) return;
        Prefs.SttModel = GroqClient.SttModels[SttModelCombo.SelectedIndex];
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || LanguageCombo.SelectedIndex < 0) return;
        Prefs.Language = GroqClient.Languages[LanguageCombo.SelectedIndex];
    }

    private void OnStoreAudioChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.StoreAudio = StoreAudioCheck.IsChecked == true;
    }

    private void OnNotifyInsertChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.NotifyOnInsert = NotifyInsertCheck.IsChecked == true;
    }

    private void OnCheckUpdatesChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.CheckUpdatesOnLaunch = CheckUpdatesCheck.IsChecked == true;
    }

    private void OnRetentionChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        if (int.TryParse(RetentionBox.Text, out int days) && days >= 0)
            Prefs.RetentionDays = days;
        else
            RetentionBox.Text = Prefs.RetentionDays.ToString();   // revert invalid input
    }

    private void OnVocabularyChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.Vocabulary = VocabularyBox.Text;
    }

    private void OnVocabularyTextChanged(object sender, RoutedEventArgs e) => UpdateVocabCounter();

    /// <summary>Live counter "N / budget" (spec §11); warning color when over — only the tail is sent.</summary>
    private void UpdateVocabCounter()
    {
        int len = VocabularyBox.Text.Trim().Length;
        VocabCounter.Text = string.Format(S.VocabCounterFmt, len, GroqClient.PromptCharBudget);
        VocabCounter.Foreground = len > GroqClient.PromptCharBudget
            ? StatusBrush("SettingsWarning")
            : StatusBrush("SettingsMuted");
    }

    // --- AI term correction (spec §6.1) ---

    // --- Rules that change words (spec §6.2/§6.3/§6.4) ---

    private void OnFillersChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.RemoveFillers = FillersCheck.IsChecked == true;
    }

    private void OnQuotesChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.FixQuotes = QuotesCheck.IsChecked == true;
    }

    private void OnTermRulesChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.FixTermsByRules = TermRulesCheck.IsChecked == true;
    }

    private void OnLlmChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.LlmPostProcess = LlmCheck.IsChecked == true;
        RefreshCloudSettings();
        LlmStatusText.Text = "";
        ChatModelPanel.Visibility = Prefs.LlmPostProcess ? Visibility.Visible : Visibility.Collapsed;
        if (Prefs.LlmPostProcess)
            _ = ProbeChatModelAsync();
    }

    private void OnChatModelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || ChatModelCombo.SelectedIndex < 0) return;
        // Index 0 is "Recommended (automatic)"; the rest are live model ids.
        Prefs.ChatModel = ChatModelCombo.SelectedIndex == 0
            ? ChatModels.Auto
            : (string)ChatModelCombo.Items[ChatModelCombo.SelectedIndex];
        _ = ProbeChatModelAsync();
    }

    /// <summary>
    /// Refreshes the live model list, re-resolves (self-healing) and probes the resolved model
    /// (spec §6.1 UX). Never blocks the UI.
    /// </summary>
    private async System.Threading.Tasks.Task ProbeChatModelAsync()
    {
        var key = KeyStore.Load();
        if (key is null)
        {
            LlmStatusText.Text = string.Format(S.LlmUnavailableFmt, S.KeyNone);
            LlmStatusText.Foreground = StatusBrush("SettingsError");
            return;
        }

        LlmStatusText.Text = S.LlmChecking;
        LlmStatusText.Foreground = StatusBrush("SettingsMuted");

        var check = await GroqClient.CheckChatModelAsync(key);
        if (check.Available)
        {
            LlmStatusText.Text = check.SteppedFrom is not null
                ? string.Format(S.LlmSteppedDownFmt, check.SteppedFrom, check.Model)
                : check.Switched
                    ? string.Format(S.LlmSwitchedFmt, check.Model)
                    : string.Format(S.LlmAvailableFmt, check.Model);
            LlmStatusText.Foreground = check.SteppedFrom is not null || check.Switched
                ? StatusBrush("SettingsWarning")
                : StatusBrush("SettingsAccent");
        }
        else
        {
            LlmStatusText.Text = string.Format(S.LlmUnavailableFmt, check.Problem);
            LlmStatusText.Foreground = StatusBrush("SettingsError");
        }

        await RefreshChatModelListAsync(key);
    }

    /// <summary>Fills the picker with "auto" + the live chat models, selecting the current choice.</summary>
    private async System.Threading.Tasks.Task RefreshChatModelListAsync(string key)
    {
        var ids = ChatModels.FilterChatModels(await GroqClient.ListModelsAsync(key)).OrderBy(x => x).ToList();
        var items = new List<string> { S.ChatModelAuto };
        items.AddRange(ids);

        var wasLoaded = _loaded;
        _loaded = false;
        ChatModelCombo.ItemsSource = items;
        var chosen = Prefs.ChatModel;
        ChatModelCombo.SelectedIndex = chosen == ChatModels.Auto ? 0 : Math.Max(0, items.IndexOf(chosen));
        _loaded = wasLoaded;
    }

    // --- Groq API key (Save-gated) ---

    /// <summary>The key currently typed, from whichever box (masked / shown) is visible.</summary>
    private string CurrentKey =>
        (ShowKeyCheck.IsChecked == true ? KeyBoxVisible.Text : KeyBox.Password).Trim();

    private void OnToggleShowKey(object sender, RoutedEventArgs e)
    {
        if (ShowKeyCheck.IsChecked == true)
        {
            KeyBoxVisible.Text = KeyBox.Password;
            KeyBoxVisible.Visibility = Visibility.Visible;
            KeyBox.Visibility = Visibility.Collapsed;
        }
        else
        {
            KeyBox.Password = KeyBoxVisible.Text;
            KeyBox.Visibility = Visibility.Visible;
            KeyBoxVisible.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshKeyStatus()
    {
        if (File.Exists(Paths.CredentialsFile))
            KeyStatusText.Text = S.KeySaved;
        else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GROQ_API_KEY")))
            KeyStatusText.Text = S.KeyEnv;
        else
            KeyStatusText.Text = S.KeyNone;
        RefreshNoKeyHint();
        RefreshSetupHints();
    }

    /// <summary>
    /// The first run without a key must not read as a dead end (spec §11.3): the local engine needs
    /// neither key nor internet, and that is said right under the key field, where the cursor is on
    /// first launch. Shown only while the cloud engine is selected AND there is no key — with a key
    /// the cloud works, with the local engine the key is not needed.
    /// </summary>
    private void RefreshNoKeyHint()
    {
        bool cloudSelected = EngineCombo.SelectedIndex != 1;
        bool hasKey = KeyStore.Load() is not null;
        NoKeyHint.Visibility = cloudSelected && !hasKey ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnValidateKey(object sender, RoutedEventArgs e)
    {
        var key = CurrentKey;
        if (key.Length == 0) key = KeyStore.Load() ?? "";
        if (key.Length == 0)
        {
            KeyStatusText.Text = S.KeyEnterValidate;
            return;
        }

        ValidateButton.IsEnabled = false;
        SaveKeyButton.IsEnabled = false;
        KeyStatusText.Text = S.KeyValidating;
        try
        {
            var result = await GroqClient.ValidateKeyAsync(key);
            KeyStatusText.Text = result.Status == KeyStatus.Valid
                ? S.KeyValidOk
                : string.Format(S.KeyInvalidFmt, result.Message);
        }
        finally
        {
            ValidateButton.IsEnabled = true;
            SaveKeyButton.IsEnabled = true;
        }
    }

    private void OnSaveKey(object sender, RoutedEventArgs e)
    {
        var key = CurrentKey;
        if (key.Length == 0)
        {
            KeyStatusText.Text = S.KeyEnterSave;
            return;
        }

        KeyStore.Save(key);
        // Keep the key in the box so it can still be shown/edited (matches macOS).
        RefreshKeyStatus();
        KeyStatusText.Text = S.KeySavedNow;
        Log.Info("Groq key saved");
    }

    // --- Network tab: system proxy (spec §9.5/§11.4) ---

    /// <summary>
    /// Re-reads the route each time the tab comes up: the Windows proxy settings can change while
    /// this window stays open.
    ///
    /// ⚠️ <c>SelectionChanged</c> bubbles, so every ComboBox on every tab arrives here too — hence
    /// the check that the TabControl itself is the one that changed.
    /// </summary>
    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || !ReferenceEquals(e.OriginalSource, Tabs)) return;
        RefreshProxyRoute();
    }

    private void OnSystemProxyChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Prefs.UseSystemProxy = SystemProxyCheck.IsChecked == true;
        // Net.Shared notices the change by itself and rebuilds; this line has to be told.
        RefreshProxyRoute();
        Log.Info($"network: system proxy {(Prefs.UseSystemProxy ? "on" : "off")}");
    }

    /// <summary>
    /// Says which way requests actually leave this PC. Deliberately not part of any error message
    /// (spec §9.5): errors name the proxy's ADDRESS, and where that address came from — Windows,
    /// this app, or nowhere — belongs here, in one language, next to the switch that changes it.
    /// </summary>
    private void RefreshProxyRoute()
    {
        // Groq is the busiest of the three destinations (§2, §2.5, §10); Windows can in principle
        // route them differently, but a PAC that splits them is not worth three lines of UI.
        var (source, address) = Net.Resolve(GroqClient.Endpoint);
        ProxyRouteText.Text = source switch
        {
            Net.ProxySource.Forced => string.Format(S.ProxyRouteForcedFmt,
                $"{address!.Host}:{address.Port}", Net.ProxyOverrideVariable),
            Net.ProxySource.System => string.Format(S.ProxyRouteSystemFmt, $"{address!.Host}:{address.Port}"),
            _ => Prefs.UseSystemProxy ? S.ProxyRouteDirect : S.ProxyRouteOff,
        };
    }

    // --- About tab: version, updates, links (spec §10/§12) ---

    /// <summary>Index of the About page in the restored sidebar.</summary>
    public const int AboutTabIndex = 7;

    public void ShowAboutSection()
    {
        SelectTab(AboutTabIndex);
    }

    /// <summary>Selects a tab by index (used by the tray's "About Voica" item).</summary>
    public void SelectTab(int index)
    {
        if (index >= 0 && index < Tabs.Items.Count) Tabs.SelectedIndex = index;
    }

    private string? _updateUrl;

    private async void OnCheckUpdatesNow(object sender, RoutedEventArgs e)
    {
        CheckNowButton.IsEnabled = false;
        UpdateStatusText.Text = S.LlmChecking;
        try
        {
            var result = await Updater.CheckAsync();
            Prefs.LastUpdateCheck = DateTime.UtcNow;
            switch (result.Outcome)
            {
                case UpdateOutcome.Available:
                    _updateUrl = result.Url;
                    DownloadUpdateButton.Content = string.Format(S.BtnDownloadUpdateFmt, result.Version);
                    DownloadUpdateButton.Visibility = Visibility.Visible;
                    UpdateStatusText.Text = string.Format(S.UpdateAvailableAskFmt, result.Version);
                    break;
                case UpdateOutcome.UpToDate:
                    UpdateStatusText.Text = string.Format(S.UpdateUpToDateFmt, AppInfo.Version);
                    break;
                case UpdateOutcome.NoRelease:
                    UpdateStatusText.Text = S.UpdateNoReleases;
                    break;
                default:
                    UpdateStatusText.Text = string.Format(S.UpdateErrorFmt, result.Message);
                    break;
            }
        }
        finally
        {
            CheckNowButton.IsEnabled = true;
        }
    }

    private void OnOpenUpdatePage(object sender, RoutedEventArgs e)
    {
        if (_updateUrl is not null) OpenUrl(_updateUrl);
    }

    private void OnNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private static void OpenUrl(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

    // --- Reset settings (spec §11): keeps key, history, audio, and vocabulary ---

    private void OnResetSettings(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(S.ResetMsg, S.ResetTitle,
            MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        var vocab = Prefs.Vocabulary;   // vocabulary is user content, not a setting
        Prefs.Reset();
        Prefs.Vocabulary = vocab;
        _onHotkeyChanged();
        LoadFromPrefs();
        KeyStatusText.Text = S.ResetDone;
        Log.Info("settings reset to defaults (key/history/audio/vocabulary kept)");
    }

    // --- Delete all data (spec §11) ---

    private void OnDeleteAllData(object sender, RoutedEventArgs e)
    {
        var phrase = "delete-" + Guid.NewGuid().ToString("N")[..4];
        var dialog = new DeleteDataDialog(phrase) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        Store.Shared.DeleteAll();
        KeyStore.Delete();
        TranslatorKeyStore.DeleteAll();
        Prefs.Reset();
        _onHotkeyChanged();       // re-apply default hotkey/mode
        LoadFromPrefs();
        // ⚠️ Say so when a key survives this. The file is gone, but the §9 environment fallback can
        // still hand one over, and LoadFromPrefs fills the field back in from it — "all data
        // deleted" next to a filled key field looks like the deletion quietly failed.
        KeyStatusText.Text = KeyStore.HasKey ? $"{S.AllDeleted} {S.AllDeletedEnvKey}" : S.AllDeleted;
        Log.Info($"all data deleted and settings reset (env key still present: {KeyStore.HasKey})");
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
