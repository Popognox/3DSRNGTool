﻿using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using pkm3dsRNG.RNG;
using pkm3dsRNG.Core;
using System.Windows.Forms;
using static PKHeX.Util;

namespace pkm3dsRNG
{
    public partial class MainForm : Form
    {
        #region global variables
        private string version = "0.30";

        private int ver { get { return Gameversion.SelectedIndex; } set { Gameversion.SelectedIndex = value; } }
        private Pokemon[] Pokemonlist;
        private Pokemon iPM => RNGPool.PM;
        private byte method => (byte)RNGMethod.SelectedIndex;
        private bool IsEvent => method == 1;
        private bool Gen6 => ver < 4;
        private bool Gen7 => 4 <= ver && ver < 6;
        private EncounterArea7 ea = new EncounterArea7();
        private bool IsMoon => ver == 5;
        private bool IsNight => Night.Checked;
        private int[] slotspecies => Gen7 ? ea.getSpecies(IsMoon, IsNight) : null;
        private byte modelnum => (byte)(NPC.Value + 1);
        private RNGFilters filter = new RNGFilters();
        List<int> OtherTSVList = new List<int>();
        #endregion

        public MainForm()
        {
            InitializeComponent();
        }

        #region Form Loading
        private void MainForm_Load(object sender, EventArgs e)
        {
            DGV.Columns["dgv_rand64"].DefaultCellStyle.Font = new Font("Consolas", 9);
            DGV.Columns["dgv_rand"].DefaultCellStyle.Font = new Font("Consolas", 9);
            DGV.Columns["dgv_PID"].DefaultCellStyle.Font = new Font("Consolas", 9);
            DGV.Columns["dgv_EC"].DefaultCellStyle.Font = new Font("Consolas", 9);
            DGV.Columns["dgv_status"].DefaultCellStyle.Font = new Font("Consolas", 9);
            Type dgvtype = typeof(DataGridView);
            System.Reflection.PropertyInfo dgvPropertyInfo = dgvtype.GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            dgvPropertyInfo.SetValue(DGV, true, null);

            Seed.Value = Properties.Settings.Default.Seed;
            var LastGameversion = Properties.Settings.Default.GameVersion;
            var Lastpkm = Properties.Settings.Default.PKM;
            var LastMethod = Properties.Settings.Default.Method;
            ShinyCharm.Checked = Properties.Settings.Default.ShinyCharm;
            TSV.Value = Properties.Settings.Default.TSV;
            loadlist(Properties.Settings.Default.TSVList);
            Advanced.Checked = Properties.Settings.Default.Advance;
            Status = new uint[] { Properties.Settings.Default.ST0, Properties.Settings.Default.ST1, Properties.Settings.Default.ST2, Properties.Settings.Default.ST3 };

            for (int i = 0; i < 6; i++)
                EventIV[i].Enabled = false;

            Gender.Items.AddRange(StringItem.genderstr);
            Ball.Items.AddRange(StringItem.genderstr);
            Event_Gender.Items.AddRange(StringItem.genderstr);
            Event_Nature.Items.AddRange(StringItem.naturestr);
            for (int i = 0; i <= 802; i++)
                Event_Species.Items.Add("-");
            for (int i = 0; i <= StringItem.naturestr.Length; i++)
                SyncNature.Items.Add("");

            string l = Properties.Settings.Default.Language;
            int lang = Array.IndexOf(langlist, l);
            if (lang < 0) lang = Array.IndexOf(langlist, "en");

            lindex = lang;
            ChangeLanguage(null, null);


            Gender.SelectedIndex =
            Ball.SelectedIndex =
            Ability.SelectedIndex =
            SyncNature.SelectedIndex =
            Event_Species.SelectedIndex = Event_PIDType.SelectedIndex =
            Event_Ability.SelectedIndex = Event_Gender.SelectedIndex =
            M_ability.SelectedIndex = F_ability.SelectedIndex =
            M_Items.SelectedIndex = F_Items.SelectedIndex =
            0;
            Egg_GenderRatio.SelectedIndex = 1;

            Gameversion.SelectedIndex = LastGameversion;
            RNGMethod.SelectedIndex = LastMethod;
            RNGMethod_Changed(null, null);

            FindSetting(Lastpkm);

            ByIVs.Checked = true;
        }

        private void FindSetting(int Lastpkm)
        {
            var Category = Pokemon.getCategoryList(ver, method);
            for (int i = 0; i < Category.Length; i++)
                if (Category[i].List.Any(t => t.SpecForm == Lastpkm))
                {
                    CB_Category.SelectedIndex = i;
                    Poke.SelectedValue = Lastpkm;
                    return;
                }
            CB_Category.SelectedIndex = 0;
        }

        private void LoadPKM()
        {
            Pokemonlist = Pokemon.getSpecFormList(ver, CB_Category.SelectedIndex, method);
            var List = Pokemonlist.Select(s => new Controls.ComboItem(s.ToString(), s.SpecForm));
            Poke.DisplayMember = "Text";
            Poke.ValueMember = "Value";
            Poke.DataSource = new BindingSource(List, null);
            Poke.SelectedIndex = 0;
        }

        private void LoadCategory()
        {
            ver = Math.Max(ver, 0);
            CB_Category.Items.Clear();
            var Category = Pokemon.getCategoryList(ver, method).Select(t => StringItem.Translate(t.ToString(), lindex)).ToArray();
            CB_Category.Items.AddRange(Category);
            CB_Category.SelectedIndex = 0;
            LoadPKM();
        }

        private void RefreshLocation()
        {
            if (Gen6)
                Locationlist.Clear(); // not impled
            else if (Gen7)
            {
                var locationlist = iPM.Conceptual ? LocationTable7.SMLocationList : (iPM as PKMW7)?.Location ?? null;
                if (locationlist == null) return;
                Locationlist = locationlist.Select(loc => new Controls.ComboItem(StringItem.getSMlocationstr(loc), loc)).ToList();
            }

            MetLocation.DisplayMember = "Text";
            MetLocation.ValueMember = "Value";
            MetLocation.DataSource = new BindingSource(Locationlist, null);

            LoadSpecies();
        }

        private void LoadSpecies()
        {
            int tmp = SlotSpecies.SelectedIndex;
            var species = slotspecies ?? new int[1];
            var List = species.Skip(1).Distinct().Select(SpecForm => new Controls.ComboItem(StringItem.species[SpecForm & 0x7FF], SpecForm));
            List = new[] { new Controls.ComboItem("-", 0) }.Concat(List);
            SlotSpecies.DisplayMember = "Text";
            SlotSpecies.ValueMember = "Value";
            SlotSpecies.DataSource = new BindingSource(List, null);
            if (0 <= tmp && tmp < SlotSpecies.Items.Count)
                SlotSpecies.SelectedIndex = tmp;
        }

        private void LoadSlotSpeciesInfo()
        {
            int SpecForm = (int)SlotSpecies.SelectedValue;
            if (Gen6) return; // not impled
            byte[] Slottype = EncounterArea7.SlotType[slotspecies[0]];
            List<int> Slotidx = new List<int>();
            for (int i = Array.IndexOf(slotspecies, SpecForm); i > -1; i = Array.IndexOf(slotspecies, SpecForm, i + 1))
                Slotidx.Add(i);
            for (int i = 0; i < 10; i++)
                Slot.CheckBoxItems[i + 1].Checked = Slotidx.Contains(Slottype[i]);

            SetPersonalInfo(SpecForm > 0 ? SpecForm : iPM.SpecForm, skip: SlotSpecies.SelectedIndex != 0);
        }
        #endregion

        #region Basic UI

        private void VisibleTrigger(object sender, EventArgs e)
        {
            if ((sender as Control).Visible == false)
                (sender as CheckBox).Checked = false;
        }

        private void TSV_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.TSV = (short)TSV.Value;
            Properties.Settings.Default.Save();
        }

        private void ShinyCharm_CheckedChanged(object sender, EventArgs e)
        {
            MM_CheckedChanged(null, null);
            Properties.Settings.Default.ShinyCharm = ShinyCharm.Checked;
            Properties.Settings.Default.Save();
        }

        private void Advanced_CheckedChanged(object sender, EventArgs e)
        {
            Special_th.Enabled = Timedelay.Enabled = Advanced.Checked;
            Properties.Settings.Default.Advance = Advanced.Checked;
            Properties.Settings.Default.Save();
        }

        private void Seed_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.Seed = Seed.Value;
            Properties.Settings.Default.Save();
        }

        private void GameVersion_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.GameVersion = (byte)Gameversion.SelectedIndex;

            var slotnum = new bool[Gen6 ? 12 : 10].Select((b, i) => (i + 1).ToString()).ToArray();
            Slot.Items.Clear();
            Slot.BlankText = "-";
            Slot.Items.AddRange(slotnum);
            Slot.CheckBoxItems[0].Checked = true;
            Slot.CheckBoxItems[0].Checked = false;

            RNGMethod_Changed(null, null);
        }

        private void Category_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadPKM();
            Poke_SelectedIndexChanged(null, null);
            SpecialOnly.Visible = Gen7 && CB_Category.SelectedIndex > 0;
        }

        private void SearchMethod_CheckedChanged(object sender, EventArgs e)
        {
            IVPanel.Visible = ByIVs.Checked;
            StatPanel.Visible = ByStats.Checked;
            ShowStats.Enabled = ShowStats.Checked = ByStats.Checked;
        }

        private void SyncNature_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (AlwaysSynced.Checked)
            {
                Nature.ClearSelection();
                if (SyncNature.SelectedIndex > 0)
                    Nature.CheckBoxItems[SyncNature.SelectedIndex].Checked = true;
            }
        }

        private void Reset_Click(object sender, EventArgs e)
        {
            IVlow = new int[6];
            IVup = new[] { 31, 31, 31, 31, 31, 31 };
            Stats = new int[6];
            Filter_Lv.Value = 0;

            Nature.ClearSelection();
            HiddenPower.ClearSelection();
            Slot.ClearSelection();
            Ball.SelectedIndex = Gender.SelectedIndex = Ability.SelectedIndex = 0;

            BlinkFOnly.Checked = SafeFOnly.Checked = SpecialOnly.Checked =
            ShinyOnly.Checked = DisableFilters.Checked = false;
        }

        private void RNGMethod_Changed(object sender, EventArgs e)
        {
            Properties.Settings.Default.Method = method;
            Properties.Settings.Default.Save();

            Reset_Click(null, null);
            RB_FrameRange.Checked = true;

            RNGMethod.TabPages[method].Controls.Add(this.Filters);
            RNGMethod.TabPages[method].Controls.Add(this.RNGInfo);
            if (0 == method || method == 2)
            {
                LoadCategory();
                Poke_SelectedIndexChanged(null, null);
            }

            Frame_min.Value = Gen7 && method < 3 ? 418 : 0;
            L_Ball.Visible = Ball.Visible = Gen7 && method == 3;

            L_Correction.Visible = Correction.Visible = Gen7 && method == 2;
            L_Slot.Visible = Slot.Visible = method == 2;

            Gen7timepanel.Visible =
            ByIVs.Enabled = ByStats.Enabled =
            BlinkFOnly.Visible = SafeFOnly.Visible =
            CreateTimeline.Visible = TimeSpan.Visible = Gen7 && method < 3 || MainRNGEgg.Checked;

            AroundTarget.Visible = method < 3 || MainRNGEgg.Checked;
            EggPanel.Visible = EggNumber.Visible = method == 3 && !MainRNGEgg.Checked;

            MM_CheckedChanged(null, null);

            switch (method)
            {
                case 0: Sta_Setting.Controls.Add(EnctrPanel); return;
                case 1: NPC.Value = 4; Event_CheckedChanged(null, null); return;
                case 2: Wild_Setting.Controls.Add(EnctrPanel); Timedelay.Value = 8; return;
                case 3: ByIVs.Checked = true; break;
            }
        }

        private void CreateTimeline_CheckedChanged(object sender, EventArgs e)
        {
            ConsiderDelay.Enabled = !CreateTimeline.Checked;
            if (CreateTimeline.Checked) ConsiderDelay.Checked = true;
        }

        private void NPC_ValueChanged(object sender, EventArgs e)
        {
            if (!Gen7)
                return;
            var ControlON = NPC.Value == 0 ? BlinkFOnly : SafeFOnly;
            var ControlOFF = NPC.Value == 0 ? SafeFOnly : BlinkFOnly;
            ControlON.Visible = true;
            ControlOFF.Visible = false;
        }

        // Wild RNG
        private void MetLocation_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Gen7)
            {
                ea = LocationTable7.Table.FirstOrDefault(t => t.Locationidx == (int)MetLocation.SelectedValue);
                NPC.Value = ea.NPC;
                Correction.Value = ea.Correction;

                Lv_min.Value = ea.SunMoonDifference && IsMoon ? ea.LevelMinMoon : ea.LevelMin;
                Lv_max.Value = ea.SunMoonDifference && IsMoon ? ea.LevelMaxMoon : ea.LevelMax;
            }

            LoadSpecies();
        }

        private void SlotSpecies_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (SlotSpecies.SelectedIndex > 0 && (Filter_Lv.Value > Lv_max.Value || Filter_Lv.Value < Lv_min.Value))
                Filter_Lv.Value = 0;
            LoadSlotSpeciesInfo();
        }

        private void Special_th_ValueChanged(object sender, EventArgs e)
        {
            L_Rate.Visible = Special_th.Visible = Special_th.Value > 0;
        }

        private void DayNight_CheckedChanged(object sender, EventArgs e)
        {
            if (ea.DayNightDifference)
                LoadSpecies();
        }
        #endregion

        #region DataEntry

        private void SetPersonalInfo(int Species, int Form, bool skip = false)
        {
            SyncNature.Enabled = !(iPM?.Nature < 25) && iPM.Syncable;
            if (Species == 0)
                return;

            // Load from personal table
            var t = Gen6 ? PersonalTable.ORAS.getFormeEntry(Species, Form) : PersonalTable.SM.getFormeEntry(Species, Form);
            BS = new[] { t.HP, t.ATK, t.DEF, t.SPA, t.SPD, t.SPE };
            GenderRatio.SelectedValue = t.Gender;
            Fix3v.Checked = t.EggGroups[0] == 0x0F; //Undiscovered Group

            // Load from Pokemonlist
            if (iPM == null || IsEvent || skip)
                return;
            Filter_Lv.Value = iPM.Level;
            AlwaysSynced.Checked = iPM.AlwaysSync;
            ShinyLocked.Checked = iPM.ShinyLocked;
            GenderRatio.SelectedValue = (int)iPM.GenderRatio;
            if (iPM.IVs != null)
            {
                IVlow = iPM.IVs.Select(iv => iv >= 0 ? iv : 0).ToArray();
                IVup = iPM.IVs.Select(iv => iv >= 0 ? iv : 31).ToArray();
            }
            if (!iPM.Syncable)
                SyncNature.SelectedIndex = 0;
            if (iPM.Nature < 25)
                SyncNature.SelectedIndex = iPM.Nature + 1;
            if (Gen7 && method == 0)
            {
                Timedelay.Value = (iPM as PKM7)?.Delay ?? 0;
                NPC.Value = (iPM as PKM7)?.NPC ?? 0;
                return;
            }
        }

        private void SetPersonalInfo(int SpecForm, bool skip = false) => SetPersonalInfo(SpecForm & 0x7FF, SpecForm >> 11, skip);

        private void Poke_SelectedIndexChanged(object sender, EventArgs e)
        {
            int specform = (int)(Poke.SelectedValue);
            Properties.Settings.Default.PKM = specform;
            Properties.Settings.Default.Save();
            Reset_Click(null, null);
            RNGPool.PM = Pokemonlist.FirstOrDefault(p => p.SpecForm == specform);
            SetPersonalInfo(specform);
            if (method == 2)
            {
                RefreshLocation();
                if (Gen7)
                {
                    var tmp = iPM as PKMW7;
                    Special_th.Value = tmp?.Rate?[MetLocation.SelectedIndex] ?? (byte)(CB_Category.SelectedIndex == 2 ? 50 : 0);
                }
                return;
            }
            AlwaysSynced.Enabled = iPM.Conceptual && specform == 0;
            ShinyLocked.Enabled = Fix3v.Enabled = GenderRatio.Enabled = iPM.Conceptual && (specform == 0 || specform == 151);
        }
        #endregion

        #region UI communication

        private void getsetting(IRNG rng)
        {
            dgvrowlist.Clear();
            DGV.Rows.Clear();

            filter = FilterSettings;
            RNGPool.RNGmethod = method;
            switch (RNGPool.RNGmethod)
            {
                case 0: RNGPool.sta_rng = getStaSettings(); break;
                case 1: RNGPool.event_rng = getEventSetting(); break;
                case 2: RNGPool.wild_rng = getWildSetting(); break;
                case 3: RNGPool.egg_rng = getEggRNG(); break;
            }

            int buffersize = 150;
            if (Gen7)
            {
                RNGPool.modelnumber = modelnum;
                RNGPool.IsSolgaleo = method == 0 && iPM.Species == 791;
                RNGPool.IsLunala = method == 0 && iPM.Species == 792;
                RNGPool.SolLunaReset = (RNGPool.IsSolgaleo || RNGPool.IsLunala) && RNGPool.modelnumber == 7;
                RNGPool.delaytime = (int)Timedelay.Value / 2;
                RNGPool.route17 = ModelStatus.route17 = method == 2 && ea.Location == 120;
                RNGPool.PreHoneyCorrection = (int)Correction.Value;

                if (method == 2)
                    buffersize += RNGPool.modelnumber * 100;
                if (RNGPool.Considerdelay = ConsiderDelay.Checked)
                    buffersize += RNGPool.modelnumber * RNGPool.delaytime;
            }
            RNGPool.CreateBuffer(buffersize, rng);
        }

        private RNGFilters FilterSettings => new RNGFilters
        {
            Nature = Nature.CheckBoxItems.Skip(1).Select(e => e.Checked).ToArray(),
            HPType = HiddenPower.CheckBoxItems.Skip(1).Select(e => e.Checked).ToArray(),
            Gender = (byte)Gender.SelectedIndex,
            Ability = (byte)Ability.SelectedIndex,
            IVlow = IVlow,
            IVup = IVup,
            BS = ByStats.Checked ? BS : null,
            Stats = ByStats.Checked ? Stats : null,
            ShinyOnly = ShinyOnly.Checked,
            Skip = DisableFilters.Checked,
            Level = (byte)Filter_Lv.Value,
            PerfectIVs = (byte)PerfectIVs.Value,

            Slot = Slot.CheckBoxItems.Select(e => e.Checked).ToArray(),
            SpecialOnly = SpecialOnly.Checked,

            BlinkFOnly = BlinkFOnly.Checked,
            SafeFOnly = SafeFOnly.Checked,

            Ball = (byte)Ball.SelectedIndex,
        };

        private StationaryRNG getStaSettings()
        {
            StationaryRNG setting = Gen6 ? (StationaryRNG)new Stationary6() : (StationaryRNG)new Stationary7();
            setting.Synchro_Stat = (byte)(SyncNature.SelectedIndex - 1);
            setting.TSV = (int)TSV.Value;
            setting.ShinyCharm = ShinyCharm.Checked;
            // Load from template
            if (!iPM.Conceptual)
            {
                setting.UseTemplate(RNGPool.PM);
                return setting;
            }

            // Load from UI
            int gender = (int)GenderRatio.SelectedValue;
            setting.IV3 = Fix3v.Checked;
            setting.Gender = FuncUtil.getGenderRatio(gender);
            setting.RandomGender = FuncUtil.IsRandomGender(gender);
            setting.AlwaysSync = AlwaysSynced.Checked;
            setting.Level = (byte)Filter_Lv.Value;
            setting.IsShinyLocked = ShinyLocked.Checked;

            return setting;
        }

        private WildRNG getWildSetting()
        {
            WildRNG setting = Gen6 ? null : new Wild7();
            setting.Synchro_Stat = (byte)(SyncNature.SelectedIndex - 1);
            setting.TSV = (int)TSV.Value;
            setting.ShinyCharm = ShinyCharm.Checked;

            int slottype = 0;
            if (Gen7)
            {
                var setting7 = setting as Wild7;
                if (ea.Locationidx == 1190) slottype = 1;
                setting7.Levelmin = (byte)Lv_min.Value;
                setting7.Levelmax = (byte)Lv_max.Value;
                setting7.SpecialEnctr = (byte)Special_th.Value;
                setting7.UB = CB_Category.SelectedIndex == 1;
                setting7.SpecForm = new int[11];
                for (int i = 1; i < 11; i++)
                    setting7.SpecForm[i] = slotspecies[EncounterArea7.SlotType[slotspecies[0]][i - 1]];
                if (setting7.SpecialEnctr > 0)
                {
                    setting7.SpecForm[0] = iPM.SpecForm;
                    setting7.SpecialLevel = iPM.Level;
                }
            }
            else if (Gen6)
            {
                slottype = 2;
            }

            setting.Markslots();
            setting.SlotSplitter = WildRNG.SlotDistribution[slottype];

            return setting;
        }

        private EggRNG getEggRNG()
        {
            var setting = Gen6 ? null : new Egg7();
            setting.FemaleIVs = IV_Female;
            setting.MaleIVs = IV_Male;
            setting.MaleItem = (byte)M_Items.SelectedIndex;
            setting.FemaleItem = (byte)F_Items.SelectedIndex;
            setting.ShinyCharm = ShinyCharm.Checked;
            setting.TSV = (short)TSV.Value;
            setting.Gender = FuncUtil.getGenderRatio((int)Egg_GenderRatio.SelectedValue);
            setting.RandomGender = FuncUtil.IsRandomGender((int)Egg_GenderRatio.SelectedValue);
            (setting as Egg7).Homogeneous = !Heterogeneity.Checked;
            (setting as Egg7).FemaleIsDitto = F_ditto.Checked;
            setting.InheritAbilty = (byte)(F_ditto.Checked ? M_ability.SelectedIndex : F_ability.SelectedIndex);
            setting.MMethod = MM.Checked;

            setting.ConsiderOtherTSV = ConsiderOtherTSV.Checked;
            setting.OtherTSVs = OtherTSVList.ToArray();

            setting.MarkItem();
            return setting;
        }

        #endregion

        #region Start Calculation
        private void AdjustDGVColumns()
        {
            dgv_synced.Visible = method < 3;
            dgv_Lv.Visible = dgv_slot.Visible = method == 2;
            dgv_rand.Visible = Gen6 || Gen7 && method == 3;
            dgv_status.Visible = dgv_ball.Visible = Gen7 && method == 3;
            dgv_adv.Visible = method == 3 && !MainRNGEgg.Checked;
            dgv_delay.Visible = dgv_mark.Visible = dgv_rand64.Visible = Gen7 && method < 3 || MainRNGEgg.Checked;
            dgv_eggnum.Visible = EggNumber.Checked;
            dgv_pid.Visible = dgv_psv.Visible = !MainRNGEgg.Visible || MainRNGEgg.Checked;
        }

        private void CalcList_Click(object sender, EventArgs e)
        {
            if (ivmin0.Value > ivmax0.Value)
                Error(SETTINGERROR_STR[lindex] + L_H.Text);
            else if (ivmin1.Value > ivmax1.Value)
                Error(SETTINGERROR_STR[lindex] + L_A.Text);
            else if (ivmin2.Value > ivmax2.Value)
                Error(SETTINGERROR_STR[lindex] + L_B.Text);
            else if (ivmin3.Value > ivmax3.Value)
                Error(SETTINGERROR_STR[lindex] + L_C.Text);
            else if (ivmin4.Value > ivmax4.Value)
                Error(SETTINGERROR_STR[lindex] + L_D.Text);
            else if (ivmin5.Value > ivmax5.Value)
                Error(SETTINGERROR_STR[lindex] + L_S.Text);
            else if (Frame_min.Value > Frame_max.Value)
                Error(SETTINGERROR_STR[lindex] + RB_FrameRange.Text);
            else
                try
                {
                    if (Gen6)
                        Search6();
                    else
                        Search7();
                    AdjustDGVColumns();
                }
                catch
                {
                    Error("Not Impled");
                }
        }

        private static readonly string[] blinkmarks = { "-", "★", "?", "? ★" };
        private DataGridViewRow getRow(int i, RNGResult result, int eggnum = -1)
        {
            DataGridViewRow row = new DataGridViewRow();
            row.CreateCells(DGV);

            string true_nature = StringItem.naturestr[result.Nature];
            if (((result as EggResult)?.BE_InheritParents ?? null) != null)
                true_nature = ((result as EggResult)?.BE_InheritParents == true) ? M_ditto.Text : F_ditto.Text;
            string EggNum = eggnum > 0 ? eggnum.ToString() : "";
            string advance = (result as EggResult)?.FramesUsed.ToString("+#;-#;0") ?? "";
            string delay = (result as Result7)?.frameshift.ToString("+#;-#;0") ?? "";
            byte blink = (result as Result7)?.Blink ?? 0;
            string Mark = blink < 4 ? blinkmarks[blink] : blink.ToString();
            string SynchronizeFlag = result.Synchronize ? "O" : "X";
            string PSV = result.PSV.ToString("D4");
            string slots = (result as WildResult)?.IsSpecial ?? false ? StringItem.gen7wildtypestr[CB_Category.SelectedIndex] : (result as WildResult)?.Slot.ToString() ?? "";
            string Lv = result.Level == 0 ? "-" : result.Level.ToString();
            string ball = PARENTS_STR[lindex, (result as EggResult)?.Ball ?? 0];
            string randstr = (result as Result6)?.RandNum.ToString("X8") ?? (result as EggResult)?.RandNum.ToString("X8") ?? "";
            string rand64str = (result as Result7)?.RandNum.ToString("X16") ?? "";
            string PID = result.PID.ToString("X8");
            string EC = result.EC.ToString("X8");
            var seedstatus = (result as EggResult)?.Status ?? new uint[1];
            string seed = string.Join(",", seedstatus.Select(v => v.ToString("X8")).Reverse());

            int[] Status = ShowStats.Checked ? result.Stats : result.IVs;

            row.SetValues(
                eggnum, i, Mark, delay, advance,
                Status[0], Status[1], Status[2], Status[3], Status[4], Status[5],
                true_nature, SynchronizeFlag, StringItem.hpstr[result.hiddenpower + 1], PSV, StringItem.genderstr[result.Gender], StringItem.abilitystr[result.Ability],
                slots, Lv, ball,
                randstr, rand64str, PID, EC, seed
                );

            if (result.Shiny)
                row.DefaultCellStyle.BackColor = Color.LightCyan;

            bool?[] ivsflag = (result as EggResult)?.InheritMaleIV ?? null;
            const int ivstart = 5;
            if (ivsflag != null)
            {
                for (int k = 0; k < 6; k++)
                {
                    if (ivsflag[k] == null)
                        continue;
                    row.Cells[ivstart + k].Style.ForeColor = (ivsflag[k] == true) ? Color.Blue : Color.DeepPink;
                }
                return row;
            }
            Font BoldFont = new Font("Microsoft Sans Serif", 8, FontStyle.Bold);
            for (int k = 0; k < 6; k++)
            {
                if (result.IVs[k] < 1)
                {
                    row.Cells[ivstart + k].Style.Font = BoldFont;
                    row.Cells[ivstart + k].Style.ForeColor = Color.OrangeRed;
                }
                else if (result.IVs[k] > 29)
                {
                    row.Cells[ivstart + k].Style.Font = BoldFont;
                    row.Cells[ivstart + k].Style.ForeColor = Color.MediumSeaGreen;
                }
            }
            return row;
        }
        #endregion

        private void Search6()
        {
            var rng = new MersenneTwister((uint)Seed.Value);
            int max, min;
            min = (int)Frame_min.Value;
            max = (int)Frame_max.Value;
            if (AroundTarget.Checked)
            {
                min = (int)Frame_max.Value - 100; max = (int)Frame_max.Value + 100;
            }
            // Advance
            for (int i = 0; i < min; i++)
                rng.Next();
            // Prepare
            getsetting(rng);
            // Start
            for (int i = min; i <= max; i++, RNGPool.Next(rng.Nextuint()))
            {
                RNGResult result = RNGPool.Generate6();
                if (!filter.CheckResult(result))
                    continue;
                dgvrowlist.Add(getRow(i, result));
                if (dgvrowlist.Count > 100000)
                    break;
            }
            DGV.Rows.AddRange(dgvrowlist.ToArray());
            DGV.CurrentCell = null;
        }

        #region Gen7 Search
        private void Search7()
        {
            if (method == 3)
            {
                if (EggNumber.Checked)
                    Search7_EggList();
                else
                    Search7_Egg();
                return;
            }
            if (CreateTimeline.Checked)
            {
                Search7_Timeline(); // method 0-2
                return;
            }
            Search7_Normal();
        }

        private void Search7_Normal()
        {
            SFMT sfmt = new SFMT((uint)Seed.Value);
            int min = (int)Frame_min.Value;
            int max = (int)Frame_max.Value;
            if (AroundTarget.Checked)
            {
                min = (int)Frame_max.Value - 100; max = (int)Frame_max.Value + 100;
            }
            // Blinkflag
            FuncUtil.getblinkflaglist(min, max, sfmt, modelnum);
            // Advance
            int StartFrame = (int)Frame_min.Value;
            for (int i = 0; i < StartFrame; i++)
                sfmt.Next();
            // Prepare
            ModelStatus status = new ModelStatus(modelnum, sfmt);
            ModelStatus stmp = new ModelStatus(modelnum, sfmt);
            getsetting(sfmt);
            int frameadvance;
            int realtime = 0;
            // Start
            for (int i = StartFrame; i <= max;)
            {
                status.CopyTo(stmp);
                frameadvance = status.NextState();

                while (frameadvance > 0)
                {
                    RNGPool.CopyStatus(stmp);
                    var result = RNGPool.Generate7() as Result7;

                    RNGPool.Next(sfmt.Nextulong());

                    frameadvance--;
                    i++;
                    if (i <= min || i > max)
                        continue;

                    FuncUtil.MarkResults(result, i - min - 1, realtime);

                    if (!filter.CheckResult(result))
                        continue;
                    dgvrowlist.Add(getRow(i - 1, result));
                }
                realtime++;
                if (dgvrowlist.Count > 100000) break;
            }
            DGV.Rows.AddRange(dgvrowlist.ToArray());
            DGV.CurrentCell = null;
        }

        private void Search7_Timeline()
        {
            SFMT sfmt = new SFMT((uint)Seed.Value);
            int start_frame = (int)Frame_min.Value;
            FuncUtil.getblinkflaglist(start_frame, start_frame, sfmt, modelnum);
            // Advance
            for (int i = 0; i < start_frame; i++)
                sfmt.Next();
            // Prepare
            ModelStatus status = new ModelStatus(modelnum, sfmt);
            getsetting(sfmt);
            int totaltime = (int)TimeSpan.Value * 30;
            int frame = (int)Frame_min.Value;
            int frameadvance, Currentframe;
            // Start
            for (int i = 0; i <= totaltime; i++)
            {
                Currentframe = frame;

                RNGPool.CopyStatus(status);

                var result = RNGPool.Generate7() as Result7;
                FuncUtil.MarkResults(result, i, i);

                frameadvance = status.NextState();
                frame += frameadvance;
                for (int j = 0; j < frameadvance; j++)
                    RNGPool.Next(sfmt.Nextulong());

                if (!filter.CheckResult(result))
                    continue;

                dgvrowlist.Add(getRow(Currentframe, result));

                if (dgvrowlist.Count > 100000)
                    break;
            }
            DGV.Rows.AddRange(dgvrowlist.ToArray());
            DGV.CurrentCell = null;
        }

        private void Search7_Egg()
        {
            var rng = new TinyMT(Status);
            int max, min;
            min = (int)Frame_min.Value;
            max = (int)Frame_max.Value;
            // Advance
            for (int i = 0; i < min; i++)
                rng.Next();
            // Prepare
            TinyMT Seedrng = (TinyMT)rng.DeepCopy();
            getsetting(rng);
            // Start
            for (int i = min; i <= max; i++, RNGPool.Next(rng.Nextuint()), Seedrng.Next())
            {
                var result = RNGPool.GenerateEgg7() as EggResult;
                if (!filter.CheckResult(result))
                    continue;
                result.Status = (uint[])Seedrng.status.Clone();
                dgvrowlist.Add(getRow(i, result));
                if (dgvrowlist.Count > 100000)
                    break;
            }
            DGV.Rows.AddRange(dgvrowlist.ToArray());
            DGV.CurrentCell = null;
        }

        private void Search7_EggList()
        {
            var rng = new TinyMT(Status);
            int max, min;
            min = (int)Egg_min.Value;
            max = (int)Egg_max.Value;
            int target = (int)TargetFrame.Value;
            bool gotresult = false;
            // Advance
            for (int i = 0; i < min; i++)
                rng.Next();
            TinyMT Seedrng = (TinyMT)rng.DeepCopy();
            // Prepare
            getsetting(rng);
            // Start
            int frame = 0;
            int advance = 0;
            for (int i = 0; i <= max; i++)
            {
                var result = RNGPool.GenerateEgg7() as EggResult;
                result.Status = (uint[])Seedrng.status.Clone();
                advance = result.FramesUsed;
                if (!gotresult && frame <= target && target < frame + advance)
                {
                    Egg_Instruction.Text = getEggListString(i, target - frame);
                    gotresult = true;
                };
                frame += advance;
                for (int j = advance; j > 0; j--)
                {
                    Seedrng.Next();
                    RNGPool.Next(rng.Nextuint());
                }
                if (i < min || !filter.CheckResult(result))
                    continue;
                dgvrowlist.Add(getRow(frame - advance, result, eggnum: i + 1));
                if (dgvrowlist.Count > 100000)
                    break;
            }
            DGV.Rows.AddRange(dgvrowlist.ToArray());
            DGV.CurrentCell = null;
            if (!gotresult)
                Egg_Instruction.Text = getEggListString(-1, -1);
        }
        #endregion
    }
}