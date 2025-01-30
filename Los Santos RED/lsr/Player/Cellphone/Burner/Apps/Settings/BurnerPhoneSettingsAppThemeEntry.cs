﻿using LosSantosRED.lsr.Interface;
using Rage;
using Rage.Native;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

public class BurnerPhoneSettingsThemeEntry : BurnerPhoneSettingsAppEntry
{
    private List<BurnerPhoneSettingTracker> BurnerPhoneSettingTrackers;
    private List<CellphoneIDLookup> Themes = new List<CellphoneIDLookup>();
    public BurnerPhoneSettingsThemeEntry(BurnerPhoneSettingsApp burnerPhoneSettingsApp, ISettingsProvideable settings, string name, int index, int icon) : base(burnerPhoneSettingsApp, settings, name, index, icon)
    {
        SelectedItemIcon = (int)BurnerPhoneSettingsIcon.Ticked;// 39;
        NonSelectedItemIcon = (int)BurnerPhoneSettingsIcon.Edit; //0;
    }
    private void GetThemes()
    {
        Themes.Clear();
        if (BurnerPhoneSettingsApp.Player.CellPhone.CurrentCellphoneData == null)
        {
            Themes.Add(new CellphoneIDLookup(1, "Blue"));
            Themes.Add(new CellphoneIDLookup(2, "Green"));
            Themes.Add(new CellphoneIDLookup(3, "Red"));
            Themes.Add(new CellphoneIDLookup(4, "Orange"));
            Themes.Add(new CellphoneIDLookup(5, "Gray"));
            Themes.Add(new CellphoneIDLookup(6, "Purple"));
            Themes.Add(new CellphoneIDLookup(7, "Pink"));
        }
        else
        {
            Themes.AddRange(BurnerPhoneSettingsApp.Player.CellPhone.CurrentCellphoneData.Themes);
        }
    }
    public override void Open(bool Reset)
    {
        BurnerPhoneSettingsApp.BurnerPhone.SetHeader(Text);
        if (Reset)
        {
            CurrentRow = 0;
        }
        NativeFunction.Natives.BEGIN_SCALEFORM_MOVIE_METHOD(BurnerPhoneSettingsApp.BurnerPhone.GlobalScaleformID, "SET_DATA_SLOT_EMPTY");
        NativeFunction.Natives.xC3D0841A0CC546A6(22);//2
        NativeFunction.Natives.END_SCALEFORM_MOVIE_METHOD();

        DisplayThemes();

        NativeFunction.Natives.BEGIN_SCALEFORM_MOVIE_METHOD(BurnerPhoneSettingsApp.BurnerPhone.GlobalScaleformID, "DISPLAY_VIEW");
        NativeFunction.Natives.xC3D0841A0CC546A6(22);
        NativeFunction.Natives.xC3D0841A0CC546A6(CurrentRow);
        NativeFunction.Natives.END_SCALEFORM_MOVIE_METHOD();
    }

    public override void HandleInput()
    {
        HandleIndex();
        HandleThemeSelection();
        HandleBack();
        SetRingtoneSoftKeys();
    }
    private void DisplayThemes()
    {
        BurnerPhoneSettingTrackers = new List<BurnerPhoneSettingTracker>();
        GetThemes();
        foreach(CellphoneIDLookup thingo in Themes.OrderBy(x => x.ID))
        {
            BurnerPhoneSettingTracker burnerPhoneSettingTracker = new BurnerPhoneSettingTracker(thingo.ID - 1, thingo.Name) { IntegerValue = thingo.ID };
            if (BurnerPhoneSettingsApp.Player.CellPhone.Theme == thingo.ID)
            {
                burnerPhoneSettingTracker.IsSelected = true;
            }
            BurnerPhoneSettingTrackers.Add(burnerPhoneSettingTracker);
            DrawSettingsItem(burnerPhoneSettingTracker.IsSelected ? SelectedItemIcon : NonSelectedItemIcon, burnerPhoneSettingTracker.Index, burnerPhoneSettingTracker.Name);
        }
        TotalItems = Themes.Count();
    }
    private void HandleThemeSelection()
    {
        if (NativeFunction.Natives.x91AEF906BCA88877<bool>(3, 176))//SELECT
        {
            BurnerPhoneSettingsApp.BurnerPhone.MoveFinger(5);
            BurnerPhoneSettingsApp.BurnerPhone.PlayAcceptedSound();
            BurnerPhoneSettingTracker selectedItem = BurnerPhoneSettingTrackers.FirstOrDefault(x => x.Index == CurrentRow);
            if (selectedItem == null)
            {
                return;
            }
            BurnerPhoneSettingTracker oldSelected = BurnerPhoneSettingTrackers.FirstOrDefault(x => x.IsSelected);
            if (oldSelected != null)
            {
                oldSelected.IsSelected = false;
            }
            selectedItem.IsSelected = true;
            BurnerPhoneSettingsApp.Player.CellPhone.CustomTheme = selectedItem.IntegerValue;
            BurnerPhoneSettingsApp.BurnerPhone.UpdateThemeItems();
            Open(false);
        }
    }
    protected void SetRingtoneSoftKeys()
    {
        BurnerPhoneSettingsApp.BurnerPhone.SetSoftKey((int)SoftKey.Left, SoftKeyIcon.Blank, Color.Red);
        BurnerPhoneSettingsApp.BurnerPhone.SetSoftKey((int)SoftKey.Middle, SoftKeyIcon.Select, Color.LightGreen);
        BurnerPhoneSettingsApp.BurnerPhone.SetSoftKey((int)SoftKey.Right, SoftKeyIcon.Back, Color.Red);
    }
}

