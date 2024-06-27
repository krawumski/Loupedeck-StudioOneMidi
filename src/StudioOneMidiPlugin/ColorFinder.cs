﻿namespace Loupedeck.StudioOneMidiPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection.Emit;
    using System.Text.RegularExpressions;

    // BitmapColor objects that have not been explicitly assigned to a
    // color are automatically replaced by the currently defined default color.
    // Since it is not possible to have a BitmapColor object that is not assigned
    // to a color (BitmapColor.NoColor evaluates to the same values as BitmapColor.White) and
    // it cannot be set to null, we define a new class that can be null.
    //
    public class FinderColor
    {
        public BitmapColor Color;

        public FinderColor(BitmapColor b)
        {
            this.Color = b;
        }
        public FinderColor(Byte r, Byte g, Byte b)
        {
            this.Color = new BitmapColor(r, g, b);
        }

        public static implicit operator BitmapColor(FinderColor f) => f.Color;
        public static explicit operator FinderColor(BitmapColor b) => new FinderColor(b);

        public static FinderColor Transparent => new FinderColor(BitmapColor.Transparent);
        public static FinderColor White => new FinderColor(BitmapColor.White);
        public static FinderColor Black => new FinderColor(BitmapColor.Black);
    }

    public class ColorFinder
    {
        public static readonly BitmapColor NoColor = new BitmapColor(-1, -1, -1);
        public class ColorSettings
        {
            public enum PotMode { Positive, Symmetric };
            public PotMode Mode = PotMode.Positive;
            public Boolean HideValueBar = false;
            public Boolean ShowUserButtonCircle = false;

            public FinderColor OnColor;
            public FinderColor OffColor;
            public FinderColor TextOnColor;
            public FinderColor TextOffColor;
            public String IconName, IconNameOn;
            public String Label;
            public String LabelOn;
            public String LinkedParameter;
            public Boolean LinkReversed = false;
            public Int32 DialSteps = 100;                // Number of steps for a mode dial

            public String[] MenuItems;

            // For plugin settings
            public const String strOnColor = "OnColor";
            public const String strLabel = "Label";
            public const String strLabelOn = "LabelOn";
            public const String strLinkedParameter = "LinkedParameter";
            public const String strMode = "Mode";
            public const String strShowCircle = "ShowCircle";
            //public const String[] strModeValue = { "Positive", "Symmetric" };
        }
        private static readonly Dictionary<(String PluginName, String PluginParameter), ColorSettings> ColorDict = new Dictionary<(String, String), ColorSettings>();
        private const String strColorSettingsID = "[cs]";  // for plugin settings

        private String LastPluginName, LastPluginParameter;
        private ColorSettings LastColorSettings;

        public Int32 CurrentUserPage = 0;              // For tracking the current user page position
        public Int32 CurrentChannel = 0;

        public ColorSettings DefaultColorSettings { get; private set; } = new ColorSettings
        {
            OnColor = FinderColor.Transparent,
            OffColor = FinderColor.Transparent,
            TextOnColor = FinderColor.White,
            TextOffColor = FinderColor.White
        };

        // Need to call "Init()" to populate the ColorSettings dictionary!
        public ColorFinder() { }

        // Need to call "Init()" to populate the ColorSettings dictionary!
        public ColorFinder(ColorSettings defaultColorSettings)
        {
            this.DefaultColorSettings = defaultColorSettings;
        }

        internal class S1TopControlColors : ColorSettings
        {
            public S1TopControlColors(String label = null)
            {
                this.OnColor = new FinderColor(54, 84, 122);
                this.OffColor = new FinderColor(27, 34, 37);
                this.TextOffColor = new FinderColor(58, 117, 195);
                this.Label = label;
            }
        }
        public void Init(Plugin plugin, Boolean forceReload = false)
        {
            if (forceReload)
            {
                ColorDict.Clear();
            }
            if (ColorDict.Count == 0)
            {
                this.InitColorDict();

                var settingsList = plugin.ListPluginSettings();

                foreach (var setting in settingsList)
                {
                    if (setting.StartsWith(strColorSettingsID))
                    {
                        var settingsParsed = setting.Substring(strColorSettingsID.Length).Split('|');
                        if (!ColorDict.TryGetValue((settingsParsed[0], settingsParsed[1]), out var cs))
                        {
                            cs = new ColorSettings { };
                        }
                        if (plugin.TryGetPluginSetting(settingName(settingsParsed[0], settingsParsed[1], settingsParsed[2]), out var val))
                        {
                            switch (settingsParsed[2])
                            {
                                case ColorSettings.strOnColor:
                                    cs.OnColor = new FinderColor(Convert.ToByte(val.Substring(0, 2), 16),
                                                                 Convert.ToByte(val.Substring(2, 2), 16),
                                                                 Convert.ToByte(val.Substring(4, 2), 16));
                                    break;
                                case ColorSettings.strLabel:
                                    cs.Label = val;
                                    break;
                                case ColorSettings.strLinkedParameter:
                                    cs.LinkedParameter = val;
                                    break;
                                case ColorSettings.strMode:
                                    cs.Mode = val.ParseInt32() == 0 ? ColorSettings.PotMode.Positive : ColorSettings.PotMode.Symmetric;
                                    break;
                                case ColorSettings.strShowCircle:
                                    cs.ShowUserButtonCircle = val.ParseInt32() == 1 ? true : false;
                                    break;
                            }
                        }
                    }
                }
            }
        }
        private void addLinked(String pluginName, String parameterName, String linkedParameter, 
                               String label = null, 
                               ColorSettings.PotMode mode = ColorSettings.PotMode.Positive,
                               Boolean linkReversed = false)
        {
            if (label == null) label = parameterName;
            var colorSettings = ColorDict[(pluginName, linkedParameter)];
            ColorDict.Add((pluginName, parameterName), new ColorSettings { Mode = mode,
                                                                           OnColor = colorSettings.OnColor,
                                                                           OffColor = colorSettings.OffColor,
                                                                           TextOnColor = colorSettings.TextOnColor,
                                                                           TextOffColor = colorSettings.TextOffColor,
                                                                           Label = label,
                                                                           LinkedParameter = linkedParameter,
                                                                           LinkReversed = linkReversed
                                                                         });
        }

        private ColorSettings saveLastSettings(ColorSettings colorSettings)
        {
            this.LastColorSettings = colorSettings;
            return colorSettings;
        }
        public ColorSettings getColorSettings(String pluginName, String parameterName, Boolean isUser)
        {
            if (pluginName == null || parameterName == null) return this.DefaultColorSettings;
            if (this.LastColorSettings != null && pluginName == this.LastPluginName && parameterName == this.LastPluginParameter) return this.LastColorSettings;

            this.LastPluginName = pluginName;
            this.LastPluginParameter = parameterName;

            var userPagePos = $"{this.CurrentUserPage}:{this.CurrentChannel}" + (isUser ? "U" : "");

            if (ColorDict.TryGetValue((pluginName, userPagePos), out var colorSettings) ||
                ColorDict.TryGetValue((pluginName, parameterName), out colorSettings) ||
                ColorDict.TryGetValue((pluginName, ""), out colorSettings) || 
                ColorDict.TryGetValue(("", parameterName), out colorSettings))
            {
                return this.saveLastSettings(colorSettings);
            }

            // Try partial match of plugin name.
            var partialMatchKeys = ColorDict.Keys.Where(currentKey => pluginName.StartsWith(currentKey.PluginName) && currentKey.PluginParameter == parameterName);
            if (partialMatchKeys.Count() > 0 && ColorDict.TryGetValue(partialMatchKeys.First(), out colorSettings))
            {
                return this.saveLastSettings(colorSettings);
            }

            partialMatchKeys = ColorDict.Keys.Where(currentKey => pluginName.Contains(currentKey.PluginName) && currentKey.PluginParameter == "");
            if (partialMatchKeys.Count() > 0 && ColorDict.TryGetValue(partialMatchKeys.First(), out colorSettings))
            {
                return this.saveLastSettings(colorSettings);
            }


            return this.saveLastSettings(this.DefaultColorSettings);
        }

        private BitmapColor findColor(FinderColor settingsColor, BitmapColor defaultColor) => settingsColor ?? defaultColor;

        public ColorSettings.PotMode getMode(String pluginName, String parameterName, Boolean isUser = false) => this.getColorSettings(pluginName, parameterName, isUser).Mode;
        public Boolean getShowCircle(String pluginName, String parameterName, Boolean isUser = false) => this.getColorSettings(pluginName, parameterName, isUser).ShowUserButtonCircle;

        public BitmapColor getOnColor(String pluginName, String parameterName, Boolean isUser = false) => this.findColor(this.getColorSettings(pluginName, parameterName, isUser).OnColor,
                                                                                                 this.DefaultColorSettings.OnColor);
        public BitmapColor getOffColor(String pluginName, String parameterName, Boolean isUser = false) => this.findColor(this.getColorSettings(pluginName, parameterName, isUser).OffColor,
                                                                                                  this.DefaultColorSettings.OffColor);
        public BitmapColor getTextOnColor(String pluginName, String parameterName, Boolean isUser = false) => this.findColor(this.getColorSettings(pluginName, parameterName, isUser).TextOnColor,
                                                                                                     this.DefaultColorSettings.TextOnColor);
        public BitmapColor getTextOffColor(String pluginName, String parameterName, Boolean isUser = false) => this.findColor(this.getColorSettings(pluginName, parameterName, isUser).TextOffColor,
                                                                                                      this.DefaultColorSettings.TextOffColor);
        public String getLabel(String pluginName, String parameterName, Boolean isUser = false) => this.getColorSettings(pluginName, parameterName, isUser).Label ?? parameterName;
        public String getLabelOn(String pluginName, String parameterName, Boolean isUser = false)
        {
            var cs = this.getColorSettings(pluginName, parameterName, isUser);
            return cs.LabelOn ?? cs.Label ?? parameterName;
        }
        public String getLabelShort(String pluginName, String parameterName, Boolean isUser = false) => stripLabel(this.getLabel(pluginName, parameterName, isUser));
        public String getLabelOnShort(String pluginName, String parameterName, Boolean isUser = false) => stripLabel(this.getLabelOn(pluginName, parameterName, isUser));
        public static String stripLabel(String label)
        {
            if (label.Length <= 12) return label;
            return Regex.Replace(label, "(?<!^)[aeiou](?!$)", "");
        }
        public BitmapImage getIcon(String pluginName, String parameterName)
        {
            var colorSettings = this.getColorSettings(pluginName, parameterName, false);
            if (colorSettings.IconName != null)
            {
                return EmbeddedResources.ReadImage(EmbeddedResources.FindFile($"{colorSettings.IconName}_52px.png"));
            }
            return null;
        }

        public BitmapImage getIconOn(String pluginName, String parameterName)
        {
            var colorSettings = this.getColorSettings(pluginName, parameterName, false);
            if (colorSettings.IconNameOn != null)
            {
                return EmbeddedResources.ReadImage(EmbeddedResources.FindFile($"{colorSettings.IconNameOn}_52px.png"));
            }
            return null;
        }
        public String getLinkedParameter(String pluginName, String parameterName, Boolean isUser = false) => this.getColorSettings(pluginName, parameterName, isUser).LinkedParameter;
        public Boolean getLinkReversed(String pluginName, String parameterName, Boolean isUser = false) => this.getColorSettings(pluginName, parameterName, isUser).LinkReversed;
        public Boolean hideValueBar(String pluginName, String parameterName, Boolean isUser = false) => this.getColorSettings(pluginName, parameterName, isUser).HideValueBar;
        public Boolean showUserButtonCircle(String pluginName, String parameterName, Boolean isUser = false) => this.getColorSettings(pluginName, parameterName, isUser).ShowUserButtonCircle;
        public Int32 getDialSteps(String pluginName, String parameterName, Boolean isUser = false) => this.getColorSettings(pluginName, parameterName, isUser).DialSteps;


        public static String settingName(String pluginName, String parameterName, String setting) => 
            strColorSettingsID + pluginName + "|" + parameterName + "|" + setting;

        private void InitColorDict()
        {
            ColorDict.Add(("", "Bypass"), new ColorSettings { OnColor = new FinderColor(204, 156, 107), IconName = "bypass" });

            ColorDict.Add(("Pro EQ", "Show Controls"), new S1TopControlColors(label: "Band Controls"));
            ColorDict.Add(("Pro EQ", "Show Dynamics"), new S1TopControlColors(label: "Dynamics"));
            ColorDict.Add(("Pro EQ", "High Quality"), new S1TopControlColors());
            ColorDict.Add(("Pro EQ", "View Mode"), new S1TopControlColors(label: "Curves"));
            ColorDict.Add(("Pro EQ", "LF-Active"), new ColorSettings { OnColor = new FinderColor(255, 120, 38), Label = "LF", ShowUserButtonCircle = true });
            ColorDict.Add(("Pro EQ", "MF-Active"), new ColorSettings { OnColor = new FinderColor(107, 224, 44), Label = "MF", ShowUserButtonCircle = true });
            ColorDict.Add(("Pro EQ", "HF-Active"), new ColorSettings { OnColor = new FinderColor(75, 212, 250), Label = "HF", ShowUserButtonCircle = true });
            ColorDict.Add(("Pro EQ", "LMF-Active"), new ColorSettings { OnColor = new FinderColor(245, 205, 58), Label = "LMF", ShowUserButtonCircle = true });
            ColorDict.Add(("Pro EQ", "HMF-Active"), new ColorSettings { OnColor = new FinderColor(70, 183, 130), Label = "HMF", ShowUserButtonCircle = true });
            ColorDict.Add(("Pro EQ", "LC-Active"), new ColorSettings { OnColor = new FinderColor(255, 74, 61), Label = "LC", ShowUserButtonCircle = true });
            ColorDict.Add(("Pro EQ", "HC-Active"), new ColorSettings { OnColor = new FinderColor(158, 98, 255), Label = "HC", ShowUserButtonCircle = true });
            ColorDict.Add(("Pro EQ", "LLC-Active"), new ColorSettings { OnColor = FinderColor.White, Label = "LLC", ShowUserButtonCircle = true });
            ColorDict.Add(("Pro EQ", "Global Gain"), new ColorSettings { OnColor = new FinderColor(200, 200, 200), Label = "Gain", Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("Pro EQ", "Auto Gain"), new ColorSettings { Label = "Auto" });
            this.addLinked("Pro EQ", "LF-Gain", "LF-Active", label: "LF Gain", mode: ColorSettings.PotMode.Symmetric);
            this.addLinked("Pro EQ", "LF-Frequency", "LF-Active", label: "LF Freq");
            this.addLinked("Pro EQ", "LF-Q", "LF-Active", label: "LF Q");
            this.addLinked("Pro EQ", "MF-Gain", "MF-Active", label: "MF Gain", mode: ColorSettings.PotMode.Symmetric);
            this.addLinked("Pro EQ", "MF-Frequency", "MF-Active", label: "MF Freq");
            this.addLinked("Pro EQ", "MF-Q", "MF-Active", label: "MF Q");
            this.addLinked("Pro EQ", "HF-Gain", "HF-Active", label: "HF Gain", mode: ColorSettings.PotMode.Symmetric);
            this.addLinked("Pro EQ", "HF-Frequency", "HF-Active", label: "HF Freq");
            this.addLinked("Pro EQ", "HF-Q", "HF-Active", label: "HF Q");
            this.addLinked("Pro EQ", "LMF-Gain", "LMF-Active", label: "LMF Gain", mode: ColorSettings.PotMode.Symmetric);
            this.addLinked("Pro EQ", "LMF-Frequency", "LMF-Active", label: "LMF Freq");
            this.addLinked("Pro EQ", "LMF-Q", "LMF-Active", label: "LMF Q");
            this.addLinked("Pro EQ", "HMF-Gain", "HMF-Active", label: "HMF Gain", mode: ColorSettings.PotMode.Symmetric);
            this.addLinked("Pro EQ", "HMF-Frequency", "HMF-Active", label: "HMF Freq");
            this.addLinked("Pro EQ", "HMF-Q", "HMF-Active", label: "HMF Q");
            this.addLinked("Pro EQ", "LC-Frequency", "LC-Active", label: "LC Freq");
            this.addLinked("Pro EQ", "HC-Frequency", "HC-Active", label: "HC Freq");
            ColorDict.Add(("Pro EQ", "LF-Solo"), new ColorSettings { OnColor = new FinderColor(224, 182, 69), Label = "LF Solo" });
            ColorDict.Add(("Pro EQ", "MF-Solo"), new ColorSettings { OnColor = new FinderColor(224, 182, 69), Label = "MF Solo" });
            ColorDict.Add(("Pro EQ", "HF-Solo"), new ColorSettings { OnColor = new FinderColor(224, 182, 69), Label = "HF Solo" });
            ColorDict.Add(("Pro EQ", "LMF-Solo"), new ColorSettings { OnColor = new FinderColor(224, 182, 69), Label = "LMF Solo" });
            ColorDict.Add(("Pro EQ", "HMF-Solo"), new ColorSettings { OnColor = new FinderColor(224, 182, 69), Label = "HMF Solo" });

            ColorDict.Add(("Fat Channel", "Hi Pass Filter"), new ColorSettings { Label = "Hi Pass" });
            ColorDict.Add(("Fat Channel", "Gate On"), new ColorSettings { OnColor = new FinderColor(250, 250, 193), TextOnColor = FinderColor.Black, Label = "Gate ON" });
            ColorDict.Add(("Fat Channel", "Range"), new ColorSettings { OffColor = FinderColor.Transparent, LinkedParameter = "Expander", LinkReversed = true });
            ColorDict.Add(("Fat Channel", "Expander"), new ColorSettings { OnColor = new FinderColor(193, 202, 214), TextOnColor = FinderColor.Black });
            ColorDict.Add(("Fat Channel", "Key Listen"), new ColorSettings { OnColor = new FinderColor(193, 202, 214), TextOnColor = FinderColor.Black });
            ColorDict.Add(("Fat Channel", "Compressor On"), new ColorSettings { OnColor = new FinderColor(250, 250, 193), TextOnColor = FinderColor.Black, Label = "Cmpr ON" });
            ColorDict.Add(("Fat Channel", "Attack"), new ColorSettings { OffColor = FinderColor.Transparent, LinkedParameter = "Auto", LinkReversed = true });
            ColorDict.Add(("Fat Channel", "Release"), new ColorSettings { OffColor = FinderColor.Transparent, LinkedParameter = "Auto", LinkReversed = true });
            ColorDict.Add(("Fat Channel", "Auto"), new ColorSettings { OnColor = new FinderColor(193, 202, 214), TextOnColor = FinderColor.Black });
            ColorDict.Add(("Fat Channel", "Peak Reduction"), new ColorSettings { Label = "Pk Reductn" });
            ColorDict.Add(("Fat Channel", "EQ On"), new ColorSettings { OnColor = new FinderColor(250, 250, 193), TextOnColor = FinderColor.Black, Label = "EQ ON" });
            ColorDict.Add(("Fat Channel", "Low On"), new ColorSettings { OnColor = new FinderColor(241, 84, 220), Label = "LF", ShowUserButtonCircle = true });
            ColorDict.Add(("Fat Channel", "Low-Mid On"), new ColorSettings { OnColor = new FinderColor(89, 236, 236), Label = "LMF", ShowUserButtonCircle = true });
            ColorDict.Add(("Fat Channel", "Hi-Mid On"), new ColorSettings { OnColor = new FinderColor(241, 178, 84), Label = "HMF", ShowUserButtonCircle = true });
            ColorDict.Add(("Fat Channel", "High On"), new ColorSettings { OnColor = new FinderColor(122, 240, 79), Label = "HF", ShowUserButtonCircle = true });
            this.addLinked("Fat Channel", "Low Gain", "Low On", label: "LF Gain", mode: ColorSettings.PotMode.Symmetric);
            this.addLinked("Fat Channel", "Low Freq", "Low On", label: "LF Freq");
            this.addLinked("Fat Channel", "Low Q", "Low On", label: "LMF Q");
            this.addLinked("Fat Channel", "Low-Mid Gain", "Low-Mid On", label: "LMF Gain", mode: ColorSettings.PotMode.Symmetric);
            this.addLinked("Fat Channel", "Low-Mid Freq", "Low-Mid On", label: "LMF Freq");
            this.addLinked("Fat Channel", "Low-Mid Q", "Low-Mid On", label: "LMF Q");
            this.addLinked("Fat Channel", "Hi-Mid Gain", "Hi-Mid On", label: "HMF Gain", mode: ColorSettings.PotMode.Symmetric);
            this.addLinked("Fat Channel", "Hi-Mid Freq", "Hi-Mid On", label: "HMF Freq");
            this.addLinked("Fat Channel", "Hi-Mid Q", "Hi-Mid On", label: "HMF Q");
            this.addLinked("Fat Channel", "High Gain", "High On", label: "HF Gain", mode: ColorSettings.PotMode.Symmetric);
            this.addLinked("Fat Channel", "High Freq", "High On", label: "HF Freq");
            this.addLinked("Fat Channel", "High Q", "High On", label: "HF Q");
            ColorDict.Add(("Fat Channel", "Low Boost"), new ColorSettings { OnColor = new FinderColor(241, 84, 220) });
            ColorDict.Add(("Fat Channel", "Low Atten"), new ColorSettings { OnColor = new FinderColor(241, 84, 220) });
            ColorDict.Add(("Fat Channel", "Low Frequency"), new ColorSettings { Label = "LF Freq", OnColor = new FinderColor(241, 84, 220), DialSteps = 3 });
            ColorDict.Add(("Fat Channel", "High Boost"), new ColorSettings { OnColor = new FinderColor(122, 240, 79) });
            ColorDict.Add(("Fat Channel", "High Atten"), new ColorSettings { OnColor = new FinderColor(122, 240, 79) });
            ColorDict.Add(("Fat Channel", "High Bandwidth"), new ColorSettings { Label = "Bandwidth", OnColor = new FinderColor(122, 240, 79) });
            ColorDict.Add(("Fat Channel", "Attenuation Select"), new ColorSettings { Label = "Atten Sel", OnColor = new FinderColor(122, 240, 79), DialSteps = 2 });
            ColorDict.Add(("Fat Channel", "Limiter On"), new ColorSettings { OnColor = new FinderColor(250, 250, 193), TextOnColor = FinderColor.Black, Label = "Limiter ON" });

            ColorDict.Add(("Compressor", "LookAhead"), new S1TopControlColors());
            ColorDict.Add(("Compressor", "Link Channels"), new S1TopControlColors(label: "CH Link"));
            ColorDict.Add(("Compressor", "Attack"), new ColorSettings { OffColor = FinderColor.Transparent, LinkedParameter = "Auto Speed", LinkReversed = true });
            ColorDict.Add(("Compressor", "Release"), new ColorSettings { OffColor = FinderColor.Transparent, LinkedParameter = "Auto Speed", LinkReversed = true });
            ColorDict.Add(("Compressor", "Auto Speed"), new ColorSettings { Label = "Auto" });
            ColorDict.Add(("Compressor", "Adaptive Speed"), new ColorSettings { Label = "Adaptive" });
            ColorDict.Add(("Compressor", "Gain"), new ColorSettings { Label = "Makeup", OffColor = FinderColor.Transparent, LinkedParameter = "Auto Gain", LinkReversed = true });
            ColorDict.Add(("Compressor", "Auto Gain"), new ColorSettings { Label = "Auto" });
            ColorDict.Add(("Compressor", "Sidechain LC-Freq"), new ColorSettings { Label = "Side LC", OffColor = FinderColor.Transparent, LinkedParameter = "Sidechain Filter" });
            ColorDict.Add(("Compressor", "Sidechain HC-Freq"), new ColorSettings { Label = "Side HC", OffColor = FinderColor.Transparent, LinkedParameter = "Sidechain Filter" });
            ColorDict.Add(("Compressor", "Sidechain Filter"), new ColorSettings { Label = "Filter" });
            ColorDict.Add(("Compressor", "Sidechain Listen"), new ColorSettings { Label = "Listen" });
            ColorDict.Add(("Compressor", "Swap Frequencies"), new ColorSettings { Label = "Swap" });

            ColorDict.Add(("Limiter", "Mode "), new ColorSettings { Label = "A", LabelOn = "B", OnColor = new FinderColor(40, 40, 40), OffColor = new FinderColor(40, 40, 40),
                                                                   TextOnColor = new FinderColor(171, 197, 226), TextOffColor = new FinderColor(171, 197, 226) });
            ColorDict.Add(("Limiter", "True Peak Limiting"), new S1TopControlColors(label: "True Peak"));
            this.addLinked("Limiter", "SoftClipper", "True Peak Limiting", label: " Soft Clip", linkReversed: true);
            ColorDict.Add(("Limiter", "Attack"), new ColorSettings { DialSteps = 2, HideValueBar = true } );

            ColorDict.Add(("Flanger", ""), new ColorSettings { OnColor = new FinderColor(238, 204, 103) });
            ColorDict.Add(("Flanger", "Feedback"), new ColorSettings { OnColor = new FinderColor(238, 204, 103), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("Flanger", "LFO Sync"), new ColorSettings { OnColor = new FinderColor(188, 198, 206), TextOnColor = FinderColor.Black });
            ColorDict.Add(("Flanger", "Depth"), new ColorSettings { OnColor = new FinderColor(238, 204, 103), Label = "Mix" });

            ColorDict.Add(("Phaser", ""), new ColorSettings { OnColor = new FinderColor(238, 204, 103) });
            ColorDict.Add(("Phaser", "Center Frequency"), new ColorSettings { OnColor = new FinderColor(238, 204, 103), Label = "Center" });
            ColorDict.Add(("Phaser", "Sweep Range"), new ColorSettings { OnColor = new FinderColor(238, 204, 103), Label = "Range" });
            ColorDict.Add(("Phaser", "Stereo Spread"), new ColorSettings { OnColor = new FinderColor(238, 204, 103), Label = "Spread" });
            ColorDict.Add(("Phaser", "Depth"), new ColorSettings { OnColor = new FinderColor(238, 204, 103), Label = "Mix" });
            ColorDict.Add(("Phaser", "LFO Sync"), new ColorSettings { OnColor = new FinderColor(188, 198, 206), TextOnColor = FinderColor.Black });
            ColorDict.Add(("Phaser", "Log. Sweep"), new ColorSettings { OnColor = new FinderColor(188, 198, 206), TextOnColor = FinderColor.Black });
            ColorDict.Add(("Phaser", "Soft"), new ColorSettings { OnColor = new FinderColor(188, 198, 206), TextOnColor = FinderColor.Black });

            // Waves

            ColorDict.Add(("SSLGChannel", "HP Frq"), new ColorSettings { OnColor = new FinderColor(220, 216, 207) });
            ColorDict.Add(("SSLGChannel", "LP Frq"), new ColorSettings { OnColor = new FinderColor(220, 216, 207) });
            ColorDict.Add(("SSLGChannel", "FilterSplit"), new ColorSettings { OnColor = new FinderColor(204, 191, 46), Label = "SPLIT" });
            ColorDict.Add(("SSLGChannel", "HF Gain"), new ColorSettings { OnColor = new FinderColor(177, 53, 63), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("SSLGChannel", "HF Frq"), new ColorSettings { OnColor = new FinderColor(177, 53, 63) });
            ColorDict.Add(("SSLGChannel", "HMF X3"), new ColorSettings { OnColor = new FinderColor(27, 92, 64), Label = "HMFx3" });
            ColorDict.Add(("SSLGChannel", "LF Gain"), new ColorSettings { OnColor = new FinderColor(180, 180, 180), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("SSLGChannel", "LF Frq"), new ColorSettings { OnColor = new FinderColor(180, 180, 180) });
            ColorDict.Add(("SSLGChannel", "LMF div3"), new ColorSettings { OnColor = new FinderColor(22, 97, 120), Label = "LMF/3" });
            ColorDict.Add(("SSLGChannel", "HMF Gain"), new ColorSettings { OnColor = new FinderColor(27, 92, 64), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("SSLGChannel", "HMF Frq"), new ColorSettings { OnColor = new FinderColor(27, 92, 64) });
            ColorDict.Add(("SSLGChannel", "HMF Q"), new ColorSettings { OnColor = new FinderColor(27, 92, 64), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("SSLGChannel", "LMF Gain"), new ColorSettings { OnColor = new FinderColor(22, 97, 120), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("SSLGChannel", "LMF Frq"), new ColorSettings { OnColor = new FinderColor(22, 97, 120) });
            ColorDict.Add(("SSLGChannel", "LMF Q"), new ColorSettings { OnColor = new FinderColor(22, 97, 120), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("SSLGChannel", "EQBypass"), new ColorSettings { OnColor = new FinderColor(226, 61, 80), Label = "EQ BYP" });
            ColorDict.Add(("SSLGChannel", "EQDynamic"), new ColorSettings { OnColor = new FinderColor(241, 171, 53), Label = "FLT DYN SC" });
            ColorDict.Add(("SSLGChannel", "CompRatio"), new ColorSettings { OnColor = new FinderColor(220, 216, 207), Label = "C RATIO" });
            ColorDict.Add(("SSLGChannel", "CompThresh"), new ColorSettings { OnColor = new FinderColor(220, 216, 207), Label = "C THRESH" });
            ColorDict.Add(("SSLGChannel", "CompRelease"), new ColorSettings { OnColor = new FinderColor(220, 216, 207), Label = "C RELEASE" });
            ColorDict.Add(("SSLGChannel", "CompFast"), new ColorSettings { Label = "F.ATK" });
            ColorDict.Add(("SSLGChannel", "ExpRange"), new ColorSettings { OnColor = new FinderColor(27, 92, 64), Label = "E RANGE" });
            ColorDict.Add(("SSLGChannel", "ExpThresh"), new ColorSettings { OnColor = new FinderColor(27, 92, 64), Label = "E THRESH" });
            ColorDict.Add(("SSLGChannel", "ExpRelease"), new ColorSettings { OnColor = new FinderColor(27, 92, 64), Label = "E RELEASE" });
            ColorDict.Add(("SSLGChannel", "ExpAttack"), new ColorSettings { Label = "F.ATK" });
            ColorDict.Add(("SSLGChannel", "ExpGate"), new ColorSettings { Label = "GATE" });
            ColorDict.Add(("SSLGChannel", "DynamicBypass"), new ColorSettings { OnColor = new FinderColor(226, 61, 80), Label = "DYN BYP" });
            ColorDict.Add(("SSLGChannel", "DynaminCHOut"), new ColorSettings { OnColor = new FinderColor(241, 171, 53), Label = "DYN CH OUT" });
            ColorDict.Add(("SSLGChannel", "VUInOut"), new ColorSettings { OnColor = new FinderColor(241, 171, 53), Label = "VU OUT" });

            ColorDict.Add(("RCompressor", "Threshold"), new ColorSettings { OnColor = new FinderColor(243, 132, 1) });
            ColorDict.Add(("RCompressor", "Ratio"), new ColorSettings { OnColor = new FinderColor(243, 132, 1) });
            ColorDict.Add(("RCompressor", "Attack"), new ColorSettings { OnColor = new FinderColor(243, 132, 1) });
            ColorDict.Add(("RCompressor", "Release"), new ColorSettings { OnColor = new FinderColor(243, 132, 1) });
            ColorDict.Add(("RCompressor", "Gain"), new ColorSettings { OnColor = new FinderColor(243, 132, 1), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("RCompressor", "Trim"), new ColorSettings { Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("RCompressor", "ARC / Manual"), new ColorSettings { Label = "ARC", LabelOn = "Manual", TextOnColor = new FinderColor(0, 0, 0), TextOffColor = new FinderColor(0, 0, 0) });
            ColorDict.Add(("RCompressor", "Electro / Opto"), new ColorSettings { Label = "Electro", LabelOn = "Opto", TextOnColor = new FinderColor(0, 0, 0), TextOffColor = new FinderColor(0, 0, 0) });
            ColorDict.Add(("RCompressor", "Warm / Smooth"), new ColorSettings { Label = "Warm", LabelOn = "Smooth", TextOnColor = new FinderColor(0, 0, 0), TextOffColor = new FinderColor(0, 0, 0) });

            ColorDict.Add(("RBass", "Orig. In-Out"), new ColorSettings { Label = "ORIG IN", OffColor = new FinderColor(230, 230, 230), TextOnColor = FinderColor.Black  });
            ColorDict.Add(("RBass", "Intensity"), new ColorSettings { OnColor = new FinderColor(243, 132, 1), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("RBass", "Frequency"), new ColorSettings { OnColor = new FinderColor(243, 132, 1) });
            ColorDict.Add(("RBass", "Out Gain"), new ColorSettings { Label = "Gain", OnColor = new FinderColor(243, 132, 1) });

            ColorDict.Add(("L1 limiter", "Threshold"), new ColorSettings { OnColor = new FinderColor(243, 132, 1) });
            ColorDict.Add(("L1 limiter", "Ceiling"), new ColorSettings { OnColor = new FinderColor(255, 172, 66) });
            ColorDict.Add(("L1 limiter", "Release"), new ColorSettings { OnColor = new FinderColor(54, 206, 206) });
            ColorDict.Add(("L1 limiter", "Auto Release"), new ColorSettings { Label = "AUTO", OnColor = new FinderColor(54, 206, 206) });

            ColorDict.Add(("PuigTec EQP1A", "OnOff"), new ColorSettings { Label = "IN", OnColor = new FinderColor(203, 53, 53) });
            ColorDict.Add(("PuigTec EQP1A", "LowBoost"), new ColorSettings { Label = "Low Boost", OnColor = new FinderColor(96, 116, 115) });
            ColorDict.Add(("PuigTec EQP1A", "LowAtten"), new ColorSettings { Label = "Low Atten", OnColor = new FinderColor(96, 116, 115) });
            ColorDict.Add(("PuigTec EQP1A", "HiBoost"), new ColorSettings { Label = "High Boost", OnColor = new FinderColor(96, 116, 115) });
            ColorDict.Add(("PuigTec EQP1A", "HiAtten"), new ColorSettings { Label = "High Atten", OnColor = new FinderColor(96, 116, 115) });
            ColorDict.Add(("PuigTec EQP1A", "LowFrequency"), new ColorSettings { Label = "Low Freq", OnColor = new FinderColor(96, 116, 115), DialSteps = 3 });
            ColorDict.Add(("PuigTec EQP1A", "HiFrequency"), new ColorSettings { Label = "High Freq", OnColor = new FinderColor(96, 116, 115), DialSteps = 6 });
            ColorDict.Add(("PuigTec EQP1A", "Bandwidth"), new ColorSettings { Label = "Bandwidth", OnColor = new FinderColor(96, 116, 115) });
            ColorDict.Add(("PuigTec EQP1A", "AttenSelect"), new ColorSettings { Label = "Atten Sel", OnColor = new FinderColor(96, 116, 115), DialSteps = 2 });
            ColorDict.Add(("PuigTec EQP1A", "Mains"), new ColorSettings { OnColor = new FinderColor(96, 116, 115), DialSteps = 2 });
            ColorDict.Add(("PuigTec EQP1A", "Gain"), new ColorSettings { OnColor = new FinderColor(96, 116, 115), Mode = ColorSettings.PotMode.Symmetric });

            ColorDict.Add(("Smack Attack", "Attack"), new ColorSettings { OnColor = new FinderColor(9, 217, 179), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("Smack Attack", "AttackSensitivity"), new ColorSettings { Label = "Sensitivity", OnColor = new FinderColor(9, 217, 179) });
            ColorDict.Add(("Smack Attack", "AttackDuration"), new ColorSettings { Label = "Duration", OnColor = new FinderColor(9, 217, 179) });
            ColorDict.Add(("Smack Attack", "AttackShape"), new ColorSettings { Label = "Shape", OnColor = new FinderColor(9, 217, 179), DialSteps = 2, HideValueBar = true });
            ColorDict.Add(("Smack Attack", "Sustain"), new ColorSettings { OnColor = new FinderColor(230, 172, 5), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("Smack Attack", "SustainSensitivity"), new ColorSettings { Label = "Sensitivity", OnColor = new FinderColor(230, 172, 5) });
            ColorDict.Add(("Smack Attack", "SustainDuration"), new ColorSettings { Label = "Duration", OnColor = new FinderColor(230, 172, 5) });
            ColorDict.Add(("Smack Attack", "SustainShape"), new ColorSettings { Label = "Shape", OnColor = new FinderColor(230, 172, 5), DialSteps = 2, HideValueBar = true });
            ColorDict.Add(("Smack Attack", "Guard"), new ColorSettings { OnColor = new FinderColor(0, 198, 250), DialSteps = 2, HideValueBar = true });
            ColorDict.Add(("Smack Attack", "Mix"), new ColorSettings { OnColor = new FinderColor(0, 198, 250) });
            ColorDict.Add(("Smack Attack", "Output"), new ColorSettings { OnColor = new FinderColor(0, 198, 250), Mode = ColorSettings.PotMode.Symmetric });

            ColorDict.Add(("Sibilance", "Monitor"), new ColorSettings { OnColor = new FinderColor(0, 195, 230) });
            ColorDict.Add(("Sibilance", "Lookahead"), new ColorSettings { OnColor = new FinderColor(0, 195, 230) });

            ColorDict.Add(("MondoMod", ""), new ColorSettings { OnColor = new FinderColor(102, 255, 51) });
            ColorDict.Add(("MondoMod", "AM On/Off"), new ColorSettings { Label = "AM", LabelOn = "AM ON", OnColor = new FinderColor(102, 255, 51), TextOnColor = FinderColor.Black });
            ColorDict.Add(("MondoMod", "FM On/Off"), new ColorSettings { Label = "FM", LabelOn = "FM ON", OnColor = new FinderColor(102, 255, 51), TextOnColor = FinderColor.Black });
            ColorDict.Add(("MondoMod", "Pan On/Off"), new ColorSettings { Label = "Pan", LabelOn = "FM ON", OnColor = new FinderColor(102, 255, 51), TextOnColor = FinderColor.Black });
            ColorDict.Add(("MondoMod", "Sync On/Off"), new ColorSettings { Label = "Manual", LabelOn = "Auto", OnColor = new FinderColor(181, 214, 165), TextOnColor = FinderColor.Black });
            ColorDict.Add(("MondoMod", "Waveform"), new ColorSettings { OnColor = new FinderColor(102, 255, 51), DialSteps = 4, HideValueBar = true });

            ColorDict.Add(("LoAir", "LoAir"), new ColorSettings { Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("LoAir", "Lo"), new ColorSettings { Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("LoAir", "Align"), new ColorSettings { OnColor = new FinderColor(206, 175, 43), TextOnColor = FinderColor.Black });

            ColorDict.Add(("CLA Unplugged", "Bass Color"), new ColorSettings { MenuItems = [ "OFF", "SUB", "LOWER", "UPPER" ] });
            ColorDict.Add(("CLA Unplugged", "Bass"), new ColorSettings { OnColor = new FinderColor(210, 209, 96), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("CLA Unplugged", "Treble"), new ColorSettings { OnColor = new FinderColor(210, 209, 96), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("CLA Unplugged", "Compress"), new ColorSettings { OnColor = new FinderColor(210, 209, 96), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("CLA Unplugged", "Reverb 1"), new ColorSettings { OnColor = new FinderColor(210, 209, 96), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("CLA Unplugged", "Reverb 2"), new ColorSettings { OnColor = new FinderColor(210, 209, 96), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("CLA Unplugged", "Delay"), new ColorSettings { OnColor = new FinderColor(210, 209, 96), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("CLA Unplugged", "Sensitivity"), new ColorSettings { Label = "Input Sens", OnColor = new FinderColor(210, 209, 96), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("CLA Unplugged", "Output"), new ColorSettings { OnColor = new FinderColor(210, 209, 96), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("CLA Unplugged", "PreDelay 1"), new ColorSettings { Label = "Pre Rvrb 1", OnColor = new FinderColor(210, 209, 96), DialSteps = 13 });
            ColorDict.Add(("CLA Unplugged", "PreDelay 2"), new ColorSettings { Label = "Pre Rvrb 2", OnColor = new FinderColor(210, 209, 96), DialSteps = 13 });
            ColorDict.Add(("CLA Unplugged", "PreDelay 1 On"), new ColorSettings { Label = "OFF", LabelOn = "ON", OnColor = new FinderColor(210, 209, 96), TextOnColor = FinderColor.Black });
            ColorDict.Add(("CLA Unplugged", "PreDelay 2 On"), new ColorSettings { Label = "OFF", LabelOn = "ON", OnColor = new FinderColor(210, 209, 96), TextOnColor = FinderColor.Black });
            ColorDict.Add(("CLA Unplugged", "Direct"), new ColorSettings { OnColor = new FinderColor(80, 80, 80), OffColor = new FinderColor(240, 228, 87),
                                                                           TextOnColor = FinderColor.Black, TextOffColor = FinderColor.Black });


            // Analog Obsession

            ColorDict.Add(("Rare", "Bypass"), new ColorSettings { Label = "IN", OnColor = new FinderColor(191, 0, 22) });
            ColorDict.Add(("Rare", "Low Boost"), new ColorSettings { Label = "Low Boost", OnColor = new FinderColor(93, 161, 183) });
            ColorDict.Add(("Rare", "Low Atten"), new ColorSettings { Label = "Low Atten", OnColor = new FinderColor(93, 161, 183) });
            ColorDict.Add(("Rare", "High Boost"), new ColorSettings { Label = "High Boost", OnColor = new FinderColor(93, 161, 183) });
            ColorDict.Add(("Rare", "High Atten"), new ColorSettings { Label = "High Atten", OnColor = new FinderColor(93, 161, 183) });
            ColorDict.Add(("Rare", "Low Frequency"), new ColorSettings { Label = "Low Freq", OnColor = new FinderColor(93, 161, 183), DialSteps = 3 });
            ColorDict.Add(("Rare", "High Freqency"), new ColorSettings { Label = "High Freq", OnColor = new FinderColor(93, 161, 183), DialSteps = 6 });
            ColorDict.Add(("Rare", "High Bandwidth"), new ColorSettings { Label = "Bandwidth", OnColor = new FinderColor(93, 161, 183) });
            ColorDict.Add(("Rare", "High Atten Freqency"), new ColorSettings { Label = "Atten Sel", OnColor = new FinderColor(93, 161, 183), DialSteps = 2 });

            ColorDict.Add(("LALA", "Bypass"), new ColorSettings { Label = "OFF", LabelOn = "ON", TextOnColor = new FinderColor(0, 0, 0), TextOffColor = new FinderColor(0, 0, 0), OnColor = new FinderColor(185, 182, 163) });
            ColorDict.Add(("LALA", "Gain"), new ColorSettings { Label = "GAIN", OnColor = new FinderColor(185, 182, 163) });
            ColorDict.Add(("LALA", "Peak Reduction"), new ColorSettings { Label = "REDUCTION", OnColor = new FinderColor(185, 182, 163) });
            ColorDict.Add(("LALA", "Mode"), new ColorSettings { Label = "LIMIT", LabelOn = "COMP", TextOnColor = new FinderColor(0, 0, 0), 
                                                                                                   TextOffColor = new FinderColor(0, 0, 0),
                                                                                                   OnColor = new FinderColor(185, 182, 163),
                                                                                                   OffColor = new FinderColor(185, 182, 163) });
            ColorDict.Add(("LALA", "1:3"), new ColorSettings { Label = "MIX", OnColor = new FinderColor(185, 182, 163) });
            ColorDict.Add(("LALA", "2:1"), new ColorSettings { Label = "HPF", OnColor = new FinderColor(185, 182, 163) });
            ColorDict.Add(("LALA", "MF"), new ColorSettings { OnColor = new FinderColor(185, 182, 163) });
            ColorDict.Add(("LALA", "MG"), new ColorSettings { OnColor = new FinderColor(185, 182, 163), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("LALA", "HF"), new ColorSettings { OnColor = new FinderColor(185, 182, 163) });
            ColorDict.Add(("LALA", "External Sidechain"), new ColorSettings { Label = "SIDECHAIN", OnColor = new FinderColor(185, 182, 163) });

            ColorDict.Add(("FETish", ""), new ColorSettings { OnColor = new FinderColor(24, 86, 119) });
            ColorDict.Add(("FETish", "Bypass"), new ColorSettings { Label = "IN", OnColor = new FinderColor(24, 86, 119) });
            ColorDict.Add(("FETish", "Input"), new ColorSettings { Label = "INPUT", OnColor = new FinderColor(186, 175, 176) });
            ColorDict.Add(("FETish", "Output"), new ColorSettings { Label = "OUTPUT", OnColor = new FinderColor(186, 175, 176) });
            ColorDict.Add(("FETish", "Ratio"), new ColorSettings { OnColor = new FinderColor(186, 175, 176), DialSteps = 16 });
            ColorDict.Add(("FETish", "Sidechain"), new ColorSettings { Label = "EXT", OnColor = new FinderColor(24, 86, 119) });
            ColorDict.Add(("FETish", "Mid Frequency"), new ColorSettings { Label = "MF", OnColor = new FinderColor(24, 86, 119) });
            ColorDict.Add(("FETish", "Mid Gain"), new ColorSettings { Label = "MG", OnColor = new FinderColor(24, 86, 119), Mode = ColorSettings.PotMode.Symmetric });

            ColorDict.Add(("dBComp", ""), new ColorSettings { OnColor = new FinderColor(105, 99, 94) });
            ColorDict.Add(("dBComp", "Output Gain"), new ColorSettings { Label = "Output", OnColor = new FinderColor(105, 99, 94) });
            ColorDict.Add(("dBComp", "1:4U"), new ColorSettings { Label = "EXT SC", OnColor = new FinderColor(208, 207, 203), TextOnColor = FinderColor.Black });

            ColorDict.Add(("BUSTERse", "Bypass"), new ColorSettings { Label = "MAIN", OnColor = new FinderColor(255, 254, 228), TextOnColor = FinderColor.Black });
            ColorDict.Add(("BUSTERse", "Turbo"), new ColorSettings { Label = "TURBO", OnColor = new FinderColor(255, 254, 228), TextOnColor = FinderColor.Black });
            ColorDict.Add(("BUSTERse", "XFormer"), new ColorSettings { Label = "XFORMER", OnColor = new FinderColor(255, 254, 228), TextOnColor = FinderColor.Black });
            ColorDict.Add(("BUSTERse", "Threshold"), new ColorSettings { Label = "THRESH", OnColor = new FinderColor(174, 164, 167) });
            ColorDict.Add(("BUSTERse", "Attack Time"), new ColorSettings { Label = "ATTACK", OnColor = new FinderColor(174, 164, 167), DialSteps = 5 });
            ColorDict.Add(("BUSTERse", "Ratio"), new ColorSettings { Label = "RATIO", OnColor = new FinderColor(174, 164, 167), DialSteps = 5 });
            ColorDict.Add(("BUSTERse", "Make-Up Gain"), new ColorSettings { Label = "MAKE-UP", OnColor = new FinderColor(174, 164, 167) });
            ColorDict.Add(("BUSTERse", "Release Time"), new ColorSettings { Label = "RELEASE", OnColor = new FinderColor(174, 164, 167), DialSteps = 4 });
            ColorDict.Add(("BUSTERse", "Compressor Mix"), new ColorSettings { Label = "MIX", OnColor = new FinderColor(174, 164, 167) });
            ColorDict.Add(("BUSTERse", "External Sidechain"), new ColorSettings { Label = "EXT", OnColor = new FinderColor(255, 254, 228), TextOnColor = FinderColor.Black });
            ColorDict.Add(("BUSTERse", "HF"), new ColorSettings { OnColor = new FinderColor(174, 164, 167) });
            ColorDict.Add(("BUSTERse", "Mid Gain"), new ColorSettings { Label = "MID", OnColor = new FinderColor(174, 164, 167), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("BUSTERse", "HPF"), new ColorSettings { OnColor = new FinderColor(174, 164, 167) });
            ColorDict.Add(("BUSTERse", "Boost"), new ColorSettings { Label = "TR BOOST", OnColor = new FinderColor(174, 164, 167) });
            ColorDict.Add(("BUSTERse", "Transient Tilt"), new ColorSettings { Label = "TR TILT", OnColor = new FinderColor(174, 164, 167), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("BUSTERse", "Transient Mix"), new ColorSettings { Label = "TR MIX", OnColor = new FinderColor(174, 164, 167) });

            ColorDict.Add(("BritChannel", ""), new ColorSettings { OnColor = new FinderColor(141, 134, 137), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("BritChannel", "Bypass"), new ColorSettings { Label = "IN", OnColor = new FinderColor(241, 223, 219), TextOnColor = FinderColor.Black });
            ColorDict.Add(("BritChannel", "Mic Pre"), new ColorSettings { Label = "MIC", OnColor = new FinderColor(241, 223, 219), TextOnColor = FinderColor.Black });
            ColorDict.Add(("BritChannel", "Mid Freq"), new ColorSettings { OnColor = new FinderColor(141, 134, 137), DialSteps = 6 });
            ColorDict.Add(("BritChannel", "Low Freq"), new ColorSettings { OnColor = new FinderColor(141, 134, 137), DialSteps = 4 });
            ColorDict.Add(("BritChannel", "HighPass"), new ColorSettings { Label = "High Pass", OnColor = new FinderColor(49, 81, 119), DialSteps = 4 });
            ColorDict.Add(("BritChannel", "Preamp Gain"), new ColorSettings { Label = "PRE GAIN", OnColor = new FinderColor(160, 53, 50), Mode = ColorSettings.PotMode.Symmetric });
            ColorDict.Add(("BritChannel", "Output Trim"), new ColorSettings { Label = "OUT TRIM", OnColor = new FinderColor(124, 117, 115), Mode = ColorSettings.PotMode.Symmetric });

            // Acon Digital

            ColorDict.Add(("Acon Digital Equalize 2", "Solo 1"), new ColorSettings { OnColor = new FinderColor(230, 159, 0), TextOnColor = FinderColor.Black });
            ColorDict.Add(("Acon Digital Equalize 2", "Bypass 1"), new ColorSettings { OnColor = new FinderColor(230, 159, 0), TextOnColor = FinderColor.Black });
            ColorDict.Add(("Acon Digital Equalize 2", "Frequency 1"), new ColorSettings { OnColor = new FinderColor(221, 125, 125) });
            ColorDict.Add(("Acon Digital Equalize 2", "Gain 1"), new ColorSettings { OnColor = new FinderColor(221, 125, 125) });
            ColorDict.Add(("Acon Digital Equalize 2", "Filter type 1"), new ColorSettings { Label = "Filter 1", OnColor = new FinderColor(221, 125, 125), DialSteps = 7, HideValueBar = true });
            ColorDict.Add(("Acon Digital Equalize 2", "Band width 1"), new ColorSettings { Label = "Bandwidth 1", OnColor = new FinderColor(221, 125, 125) });
            ColorDict.Add(("Acon Digital Equalize 2", "Slope 1"), new ColorSettings { OnColor = new FinderColor(221, 125, 125) });
            ColorDict.Add(("Acon Digital Equalize 2", "Resonance 1"), new ColorSettings { OnColor = new FinderColor(221, 125, 125) });
        }
    }
}
