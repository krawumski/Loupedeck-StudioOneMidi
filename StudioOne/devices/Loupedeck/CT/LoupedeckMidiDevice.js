include_file("resource://com.presonus.musicdevices/sdk/midiprotocol.js");
include_file("resource://com.presonus.musicdevices/sdk/controlsurfacedevice.js");
include_file("LoupedeckProtocol.js");

class ChannelTextHandler extends PreSonus.ControlHandler {
    constructor(name, channelID, offset) {
        super();
        this.name = name;
        this.channelID = channelID;
        this.offset = offset;
    }
    sendValue(value, flags) {
        this.device.sendChannelText(this.channelID, this.offset, value, this.control);
    }
}

class FocusDeviceTextHandler extends PreSonus.ControlHandler {
    constructor(name) {
        super();
        this.name = name;
    }
    sendValue(value, flags) {
        this.device.sendFocusDeviceText(value);
    }
}

class FunctionTextHandler extends PreSonus.ControlHandler {
    constructor(name, index) {
        super();
        this.name = name;
        this.index = index;
    }
    sendValue(value, flags) {
        this.device.sendFunctionText(this.index, value);
    }
}

class LoupedeckMidiDevice extends PreSonus.ControlSurfaceDevice {
    constructor() {
        super();
        this.debugLog = false;
    }
    onInit(hostDevice) {
        super.onInit(hostDevice);
        this.debugLog = false;
        for (let i = 0; i < 6; i++) {
            this.addHandler(new ChannelTextHandler("labelText["+i+"]", i, 0));
            this.addHandler(new ChannelTextHandler("valueText["+i+"]", i, 1));
            this.addHandler(new ChannelTextHandler("descText["+i+"]",  i, 2));
            this.addHandler(new ChannelTextHandler("userText["+i+"]",  i, 3));
        }
        this.addHandler(new ChannelTextHandler("selectedLabelText", 6, 0));
        this.addHandler(new ChannelTextHandler("selectedVolumeText", 6, 1));
        this.addHandler(new ChannelTextHandler("selectedLabelText", 7, 0));
        this.addHandler(new ChannelTextHandler("selectedPanText", 7, 1));
        for (let i = 0; i < 12; i++) {
            this.addHandler(new FunctionTextHandler("F"+(i+1) +"Text", i));
        }
        this.addHandler(new FunctionTextHandler("U1Text", 12));
        this.addHandler(new FunctionTextHandler("U2Text", 13));
        this.addHandler(new FocusDeviceTextHandler("focusDeviceText"));
    }
    onMidiOutConnected(state) {
        super.onMidiOutConnected(state);
        if (state) {
//            this.sendSysex(LoupedeckProtocol.buildNativeModeSysex(this.sysexSendBuffer));
//            this.sendText("Studio One", LoupedeckControls.kUpperRightText);
//            this.sendText("Control Link", LoupedeckControls.kLowerText);
            this.hostDevice.invalidateAll();
        }
    }
    sendChannelText(channelID, offset, text) {
        // let outText = text.substring(0, 1) + text.substring(1);
        // let outText = text;
        // if (outText.length > 8) outText = outText.replace(/(?<!^)[aeiou]/g, '');
        this.sendSysex(LoupedeckProtocol.buildChannelTextSysex(this.sysexSendBuffer, channelID, offset, text)); 
    } 
    sendFocusDeviceText(text) {
        this.sendSysex(LoupedeckProtocol.buildFocusDeviceTextSysex(this.sysexSendBuffer, text)); 
    } 
    sendFunctionText(index, text) {
        this.sendSysex(LoupedeckProtocol.buildFunctionTextSysex(this.sysexSendBuffer, index, text)); 
    } 
}

function createLoupedeckDeviceInstance() {
    return new LoupedeckMidiDevice();
}
