using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Abr.Civil.Sdk;
using AbrCivil.PlanStrip.Core.Model;
using AbrCivil.PlanStrip.Core.Presets;

namespace AbrCivil.PlanStrip.Ui
{
    /// <summary>Трасса, вид профиля, ширина, режим, обрезка, содержимое, слои, пресеты.
    /// Civil-агностичен: список трасс/видов передаётся строками, точку в Cad не знает.</summary>
    internal sealed class StripDialog : Form
    {
        private readonly ComboBox _alignment = new ComboBox();
        private readonly ComboBox _profileView = new ComboBox();
        private readonly TextBox _leftWidth = new TextBox();
        private readonly TextBox _rightWidth = new TextBox();
        private readonly ComboBox _mode = new ComboBox();
        private readonly CheckBox _clip = new CheckBox();
        private readonly TextBox _crossScale = new TextBox();
        private readonly TextBox _sagTolerance = new TextBox();
        private readonly TextBox _gapAboveGrid = new TextBox();

        private readonly CheckBox _drawAxis = new CheckBox();
        private readonly CheckBox _drawStations = new CheckBox();
        private readonly CheckBox _drawCorners = new CheckBox();
        private readonly CheckBox _drawCivilLabels = new CheckBox();
        private readonly TextBox _stationLabelStep = new TextBox();
        private readonly TextBox _layerPrefix = new TextBox();
        private readonly CheckedListBox _layers = new CheckedListBox();

        private readonly ComboBox _preset = new ComboBox();

        private readonly StripPresetStore _presets;
        private readonly Func<string, IList<KeyValuePair<string, string>>> _profileViewsFor;

        public string AlignmentName { get { return _alignment.Text; } }

        public string ProfileViewHandle
        {
            get
            {
                var selected = _profileView.SelectedItem as KeyValuePair<string, string>?;
                return selected.HasValue ? selected.Value.Value : null;
            }
        }

        public StripDialog(
            IList<string> alignmentNames,
            Func<string, IList<KeyValuePair<string, string>>> profileViewsFor,
            IList<string> layerNames,
            StripPresetStore presets,
            StripSettings initial)
        {
            _presets = presets;
            _profileViewsFor = profileViewsFor;

            Text = "Развёртка плана";
            ClientSize = new Size(470, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9f);
            Icon = AbrIcon.Create();

            _alignment.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var name in alignmentNames) _alignment.Items.Add(name);
            if (_alignment.Items.Count > 0) _alignment.SelectedIndex = 0;
            _alignment.SelectedIndexChanged += delegate { ReloadProfileViews(); };

            _profileView.DropDownStyle = ComboBoxStyle.DropDownList;
            _profileView.DisplayMember = "Key";

            _mode.DropDownStyle = ComboBoxStyle.DropDownList;
            _mode.Items.Add("Плавно");
            _mode.Items.Add("По углам");
            _mode.SelectedIndex = initial.Mode == StraightenMode.Corners ? 1 : 0;

            _clip.Text = "Обрезать по границе полосы";
            _clip.Checked = initial.Clip;

            _leftWidth.Text = ToText(initial.LeftWidth);
            _rightWidth.Text = ToText(initial.RightWidth);
            _crossScale.Text = ToText(initial.CrossScale);
            _sagTolerance.Text = ToText(initial.SagTolerance);
            _gapAboveGrid.Text = ToText(initial.GapAboveGrid);
            _stationLabelStep.Text = ToText(initial.StationLabelStep);
            _layerPrefix.Text = initial.LayerPrefix;

            _drawAxis.Text = "Ось и границы полосы";
            _drawAxis.Checked = initial.DrawAxis;
            _drawStations.Text = "Пикетаж и километровые знаки";
            _drawStations.Checked = initial.DrawStations;
            _drawCorners.Text = "Элементы кривых на углах поворота";
            _drawCorners.Checked = initial.DrawCorners;
            _drawCivilLabels.Text = "Подписи Civil-объектов";
            _drawCivilLabels.Checked = initial.DrawCivilLabels;

            _layers.CheckOnClick = true;
            foreach (var layer in layerNames)
            {
                int index = _layers.Items.Add(layer);
                _layers.SetItemChecked(index, !initial.Filter.Excluded.Contains(layer));
            }

            _preset.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var name in _presets.List()) _preset.Items.Add(name);
            _preset.Items.Insert(0, "");
            _preset.SelectedIndex = 0;
            _preset.SelectedIndexChanged += delegate { LoadPreset(); };

            var tabs = new TabControl();
            tabs.SetBounds(8, 8, 454, 360);

            tabs.TabPages.Add(Page("Трасса",
                Row("Трасса:", _alignment),
                Row("Вид профиля:", _profileView),
                Row("Ширина слева, м:", _leftWidth),
                Row("Ширина справа, м:", _rightWidth),
                Row("Режим развёртки:", _mode),
                Single(_clip),
                Row("Сжатие поперёк (1 = без сжатия):", _crossScale),
                Row("Допуск стрелки кривых:", _sagTolerance),
                Row("Зазор над сеткой, мм:", _gapAboveGrid)));

            tabs.TabPages.Add(Page("Содержимое",
                Single(_drawAxis),
                Single(_drawStations),
                Single(_drawCorners),
                Single(_drawCivilLabels),
                Row("Шаг подписей пикетажа:", _stationLabelStep),
                Row("Префикс своих слоёв:", _layerPrefix)));

            var layersPage = new TabPage("Слои источников");
            _layers.SetBounds(12, 12, 414, 300);
            layersPage.Controls.Add(_layers);
            tabs.TabPages.Add(layersPage);

            tabs.TabPages.Add(Page("Наборы оформления", Row("Набор оформления:", _preset)));

            Controls.Add(tabs);

            ReloadProfileViews();

            var ok = new Button();
            ok.Text = "Построить";
            ok.SetBounds(270, 378, 90, 28);
            ok.FlatStyle = FlatStyle.System;
            ok.DialogResult = DialogResult.OK;
            Controls.Add(ok);

            var cancel = new Button();
            cancel.Text = "Отмена";
            cancel.SetBounds(370, 378, 90, 28);
            cancel.FlatStyle = FlatStyle.System;
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        /// <summary>Настройки полосы, собранные из полей формы.</summary>
        public StripSettings BuildSettings()
        {
            var settings = new StripSettings
            {
                LeftWidth = ParseDouble(_leftWidth.Text, 50.0),
                RightWidth = ParseDouble(_rightWidth.Text, 50.0),
                Mode = _mode.SelectedIndex == 1 ? StraightenMode.Corners : StraightenMode.Smooth,
                Clip = _clip.Checked,
                CrossScale = ParseDouble(_crossScale.Text, 1.0),
                SagTolerance = ParseDouble(_sagTolerance.Text, 0.02),
                GapAboveGrid = ParseDouble(_gapAboveGrid.Text, 15.0),
                DrawAxis = _drawAxis.Checked,
                DrawStations = _drawStations.Checked,
                DrawCorners = _drawCorners.Checked,
                DrawCivilLabels = _drawCivilLabels.Checked,
                StationLabelStep = ParseDouble(_stationLabelStep.Text, 100.0),
                LayerPrefix = string.IsNullOrEmpty(_layerPrefix.Text) ? "Развёртка_" : _layerPrefix.Text
            };

            for (int i = 0; i < _layers.Items.Count; i++)
                if (!_layers.GetItemChecked(i)) settings.Filter.Excluded.Add((string)_layers.Items[i]);

            return settings;
        }

        /// <summary>Сохранить текущие настройки формы как именованный пресет
        /// (используется командой PLANSTRIPSETTINGS).</summary>
        public void SaveAsPreset(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            _presets.Save(name, BuildSettings());
            if (!_preset.Items.Contains(name)) _preset.Items.Add(name);
            _preset.SelectedItem = name;
        }

        private void LoadPreset()
        {
            string name = _preset.Text;
            if (string.IsNullOrEmpty(name)) return;

            var settings = _presets.Load(name);
            if (settings == null) return;

            _leftWidth.Text = ToText(settings.LeftWidth);
            _rightWidth.Text = ToText(settings.RightWidth);
            _mode.SelectedIndex = settings.Mode == StraightenMode.Corners ? 1 : 0;
            _clip.Checked = settings.Clip;
            _crossScale.Text = ToText(settings.CrossScale);
            _sagTolerance.Text = ToText(settings.SagTolerance);
            _gapAboveGrid.Text = ToText(settings.GapAboveGrid);
            _drawAxis.Checked = settings.DrawAxis;
            _drawStations.Checked = settings.DrawStations;
            _drawCorners.Checked = settings.DrawCorners;
            _drawCivilLabels.Checked = settings.DrawCivilLabels;
            _stationLabelStep.Text = ToText(settings.StationLabelStep);
            _layerPrefix.Text = settings.LayerPrefix;

            for (int i = 0; i < _layers.Items.Count; i++)
                _layers.SetItemChecked(i, !settings.Filter.Excluded.Contains((string)_layers.Items[i]));
        }

        private void ReloadProfileViews()
        {
            _profileView.Items.Clear();
            if (_alignment.SelectedItem == null) return;

            foreach (var pair in _profileViewsFor((string)_alignment.SelectedItem))
                _profileView.Items.Add(pair);

            if (_profileView.Items.Count > 0) _profileView.SelectedIndex = 0;
        }

        private static string ToText(double value)
        {
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static double ParseDouble(string text, double fallback)
        {
            double value;
            text = (text ?? "").Replace(',', '.');

            if (double.TryParse(text, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out value)) return value;

            return fallback;
        }

        private static Control[] Row(string caption, Control control)
        {
            var label = new Label();
            label.Text = caption;
            label.Size = new Size(220, 20);

            control.Size = new Size(190, 22);
            return new[] { label, control };
        }

        private static Control[] Single(Control control)
        {
            control.Size = new Size(414, 22);
            return new[] { control };
        }

        private static TabPage Page(string title, params Control[][] rows)
        {
            var page = new TabPage(title);
            int y = 14;

            foreach (var row in rows)
            {
                if (row.Length == 2)
                {
                    row[0].Location = new Point(12, y + 3);
                    row[1].Location = new Point(238, y);
                }
                else
                {
                    row[0].Location = new Point(12, y);
                }

                foreach (var control in row) page.Controls.Add(control);
                y += 30;
            }

            return page;
        }
    }
}
