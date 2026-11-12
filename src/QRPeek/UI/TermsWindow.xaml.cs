// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using QRPeek.Core;

namespace QRPeek.UI
{
    /// <summary>
    /// Settings → Receipt terms: the user's own labels on top of the built-in
    /// Turkish and English ones, in the same plain format ("[field] term, term").
    /// Import/export as .txt so lists can be shared; a ready-made prompt lets
    /// any AI the user picks write a list for another language. QRPeek itself
    /// never talks to an AI or the internet here.
    /// </summary>
    public partial class TermsWindow : Window
    {
        private const int MaxImportBytes = 256 * 1024;
        private const int ProblemsShown = 3;

        public TermsWindow()
        {
            InitializeComponent();
            WindowTheme.Attach(this);
            FormatText.Text = S.TermsFormat + Environment.NewLine + S.TermsFieldList(string.Join(", ", ReceiptTerms.KnownFields));
            Editor.Text = Storage.LoadUserTerms();
            if (string.IsNullOrWhiteSpace(Editor.Text)) Editor.Text = S.TermsTemplate + Environment.NewLine;
            BuiltInBox.Text = ReceiptTerms.BuiltIn.Trim();
            BuiltInToggle.Content = S.TermsShowBuiltIn;
            UpdateStatus();
            Loaded += (s, e) =>
            {
                Editor.Focus();
                Editor.CaretIndex = Editor.Text.Length;
            };
        }

        private void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateStatus();

        private void UpdateStatus()
        {
            List<ReceiptTerms.Problem> problems = ReceiptTerms.Check(Editor.Text, out int terms);
            var sb = new StringBuilder(S.TermsCount(terms));
            if (problems.Count > 0)
            {
                sb.Append("  ·  ").Append(S.TermsProblems(problems.Count));
                foreach (ReceiptTerms.Problem p in problems.Take(ProblemsShown))
                    sb.Append(Environment.NewLine).Append(S.TermsLine(p.Line, p.Message));
                if (problems.Count > ProblemsShown) sb.Append(Environment.NewLine).Append("…");
            }
            StatusText.Text = sb.ToString();
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource(problems.Count > 0 ? "Bad" : "FgMuted");
        }

        private void OnToggleBuiltIn(object sender, RoutedEventArgs e)
        {
            bool show = BuiltInBox.Visibility != Visibility.Visible;
            BuiltInBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            BuiltInToggle.Content = show ? S.TermsHideBuiltIn : S.TermsShowBuiltIn;
        }

        private void OnImport(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = S.TextFilter };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                if (new FileInfo(dlg.FileName).Length > MaxImportBytes)
                {
                    MessageBox.Show(this, S.TermsTooBig, S.TermsTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string text = File.ReadAllText(dlg.FileName, Encoding.UTF8).Trim();
                // Added below the user's own terms, never replacing them.
                Editor.Text = Editor.Text.TrimEnd() + Environment.NewLine + Environment.NewLine + text + Environment.NewLine;
                Editor.CaretIndex = Editor.Text.Length;
                Editor.ScrollToEnd();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this, S.TermsReadFailed, S.TermsTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnExport(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = S.TextFilter, FileName = "qrpeek-terms.txt" };
            if (dlg.ShowDialog(this) != true) return;
            try { File.WriteAllText(dlg.FileName, Editor.Text, new UTF8Encoding(false)); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Storage.LogError(ex); }
        }

        private void OnAiPrompt(object sender, RoutedEventArgs e)
        {
            if (!SafeClipboard.SetText(S.TermsAiPromptText(string.Join(", ", ReceiptTerms.KnownFields)))) return;
            AiText.Text = S.Copied;
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            t.Tick += (s, a) => { AiText.Text = S.TermsAiPrompt; t.Stop(); };
            t.Start();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            string text = Editor.Text.Trim() == S.TermsTemplate.Trim() ? "" : Editor.Text;
            Storage.SaveUserTerms(text);
            ReceiptTerms.UseUserTerms(text);
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
