include_file("resource://com.presonus.musicdevices/sdk/controlsurfacecomponent.js");
include_file("LoupedeckShared.js");
const kLoupedeckCTMixerBanks = [
    PreSonus.MixerConsoleBankID.kAll,
    PreSonus.MixerConsoleBankID.kAudioInput,
    PreSonus.MixerConsoleBankID.kAudioTrack,
    PreSonus.MixerConsoleBankID.kAudioSynth,
    PreSonus.MixerConsoleBankID.kAudioFX,
    PreSonus.MixerConsoleBankID.kAudioBus,
    PreSonus.MixerConsoleBankID.kAudioOutput,
    PreSonus.MixerConsoleBankID.kAudioVCA,
    PreSonus.MixerConsoleBankID.kUser
];
class LoupedeckCTComponent extends LoupedeckSharedComponent {
    onInit(hostComponent) {
        // Host.Console.writeLine("Connecting Loupedeck CT...");
        super.onInit(hostComponent);
        let paramList = hostComponent.paramList;
        this.assignMode = paramList.addInteger(0, ChannelAssignmentMode.kLastMode, "assignMode");
        this.assignString = paramList.addString("assignString");
        this.sendMode = paramList.addParam("sendMode");
        this.flipMode = paramList.addParam("flipMode");
        this.panModeLED = paramList.addParam("panModeLED");
        
        this.updateModeParams();
        
        this.bankList = paramList.addList("bankList");
        for (let i in kLoupedeckCTMixerBanks)
            this.bankList.appendString(kLoupedeckCTMixerBanks[i]);
        this.bankList.value = kLoupedeckCTMixerBanks.indexOf(PreSonus.MixerConsoleBankID.kUser);
    }
    onPanButtonPressed(value) {
        if (!value)
            return;
        let currentMode = this.assignment.mode;
        if (currentMode == ChannelAssignmentMode.kPanMode) {
            this.assignMode.setValue(ChannelAssignmentMode.kPanFocusMode, true);
            return;
        }
        else
            this.assignMode.setValue(ChannelAssignmentMode.kPanMode, true);
    }
    updateModeParams() {
        this.assignMode.value = this.assignment.mode;
        this.assignString.string = this.assignment.getModeString();
        this.flipMode.value = this.assignment.flipActive;
        this.panModeLED.value = this.assignment.isPanMode();
    }
    getMaxSendSlotCount() {
        if (this.mixerMapping.component)
            return this.mixerMapping.invokeChildMethod("audioMixer", "getMaxSlotCount", PreSonus.FolderID.kSendsFolder);
        return kNumChannels;
    }
    onSyncDevice(otherData) {
        super.onSyncDevice(otherData);
        this.updateModeParams();
    }
    paramChanged(param) {
        // Host.Console.writeLine("LoupedeckCTComponent.paramChanged");
        if (param == this.sendMode) {
            this.assignment.navigateSends(this.getMaxSendSlotCount());
            this.updateModeParams();
            this.updateAll();
            this.signalSyncDevice();
        }
        else if (param == this.assignMode) {
            this.assignment.mode = this.assignMode.value;

            this.updateModeParams();

            let mode = this.assignment.mode;
            let userBank = -1;
    
            if (mode == ChannelAssignmentMode.kUser1Mode) userBank = 0;
            else if (mode == ChannelAssignmentMode.kUser2Mode) userBank = 1;
            else if (mode == ChannelAssignmentMode.kUser3Mode) userBank = 2;
    
            if (userBank >= 0) {
                // Host.Console.writeLine("paramChanged vpot[" + userBank + "]");
                for (let i = 0; i < kNumChannels; i++) {
                    let genericMappingElement = this.root.getGenericMapping();
                    this.channels[i].plugControlElement = genericMappingElement.getElement(0).find("vpot[" + userBank + "][" + i + "]");
                    this.channels[i].plugButtonElement  = genericMappingElement.getElement(0).find("vbut[" + userBank + "][" + i + "]");
                }
            }

            this.updateAll();
            this.signalSyncDevice();
        }
        else if (param == this.flipMode) {
            this.assignment.flipActive = this.flipMode.value;
            this.updateAll();
            this.signalSyncDevice();
        }
        else if (param == this.bankList) {
            this.channelBankElement.selectBank(this.bankList.string);
        }
    }
}
function createLoupedeckCTInstance() {
    return new LoupedeckCTComponent();
}