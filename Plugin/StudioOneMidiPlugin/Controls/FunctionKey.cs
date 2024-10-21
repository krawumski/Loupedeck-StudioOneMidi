﻿namespace Loupedeck.StudioOneMidiPlugin.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Melanchall.DryWetMidi.Core;
    using static Loupedeck.StudioOneMidiPlugin.StudioOneMidiPlugin;

    internal class FunctionKey : StudioOneButton<CommandButtonData>
    {
        public FunctionKey() : base()
        {
            for (int i = 0; i < 12; i++)
            {
                this.AddButton(new CommandButtonData(0x60 + i, "F" + (i + 1), new BitmapColor(200, 200, 200), BitmapColor.Black));
            }
        }

        protected override bool OnLoad()
        {
            base.OnLoad();

            this.plugin.CommandNoteReceived += (object sender, NoteOnEvent e) =>
            {
                string param = e.NoteNumber.ToString();
                if (!this.buttonData.ContainsKey(param)) return;

                var bd = this.buttonData[param];
                bd.Activated = e.Velocity > 0;
                this.EmitActionImageChanged();
            };

            this.plugin.FunctionKeyChanged += (object sender, FunctionKeyParams fke) =>
            {
                // Need to check if there is a key in the dictionary for the received
                // parameters since the global user buttons are handled as additional
                // function keys.
                //
                if (this.buttonData.TryGetValue((fke.KeyID + 0x60).ToString(), out var bd))
                {
                    bd.Name = fke.FunctionName;
                }

                this.EmitActionImageChanged();
            };

            return true;
        }

        private void AddButton(CommandButtonData bd)
        {
            this.buttonData[bd.Code.ToString()] = bd;
            this.AddParameter(bd.Code.ToString(), bd.Name, "Function Keys");
        }
    }
}