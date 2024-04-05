class TextCell {
}
class LoupedeckControls {
}
LoupedeckControls.kLabelText0 = new TextCell();
LoupedeckControls.kLabelText1 = new TextCell();
LoupedeckControls.kLabelText2 = new TextCell();
LoupedeckControls.kLabelText3 = new TextCell();
LoupedeckControls.kLabelText4 = new TextCell();
LoupedeckControls.kLabelText5 = new TextCell();
LoupedeckControls.kValueText0 = new TextCell();
LoupedeckControls.kValueText1 = new TextCell();
LoupedeckControls.kValueText2 = new TextCell();
LoupedeckControls.kValueText3 = new TextCell();
LoupedeckControls.kValueText4 = new TextCell();
LoupedeckControls.kValueText5 = new TextCell();
LoupedeckControls.kDescText0 = new TextCell();
LoupedeckControls.kDescText1 = new TextCell();
LoupedeckControls.kDescText2 = new TextCell();
LoupedeckControls.kDescText3 = new TextCell();
LoupedeckControls.kDescText4 = new TextCell();
LoupedeckControls.kDescText5 = new TextCell();
LoupedeckControls.kUserText0 = new TextCell();
LoupedeckControls.kUserText1 = new TextCell();
LoupedeckControls.kUserText2 = new TextCell();
LoupedeckControls.kUserText3 = new TextCell();
LoupedeckControls.kUserText4 = new TextCell();
LoupedeckControls.kUserText5 = new TextCell();
LoupedeckControls.kSelectedLabelText = new TextCell();
LoupedeckControls.kSelectedValueText = new TextCell();
class LoupedeckProtocol {
    static buildNativeModeSysex(sysexBuffer) {
        sysexBuffer.begin(LoupedeckProtocol.kSysexHeader);
        sysexBuffer.push(0x10);
        sysexBuffer.push(0x01);
        sysexBuffer.push(0x00);
        sysexBuffer.end();
        return sysexBuffer;
    }
    
    // Mackie MCU style sysex message for sending a string to the display. 
    // Unlike the real MCU where text is sent directly to a position in the display,
    // channelID and offset are used to indicate the channel and value type.
    // offset 0 - Label text
    //        1 - Value text
    //        2 - Description
    //        3 - User button text
    //
    static buildChannelTextSysex(sysexBuffer, channelID, offset, text) {
        sysexBuffer.begin(LoupedeckProtocol.kSysexHeader);
        sysexBuffer.push(0x14);
        sysexBuffer.push(0x12);
        sysexBuffer.push(channelID * 4 + offset);
        sysexBuffer.appendAscii(text);
        sysexBuffer.end();
        return sysexBuffer;
    }
    static buildPlainTextSysex(sysexBuffer, text) {
        sysexBuffer.begin(LoupedeckProtocol.kSysexHeader);
        sysexBuffer.push(0x14);
        sysexBuffer.push(0x13);
        sysexBuffer.appendAscii(text);
        sysexBuffer.end();
        return sysexBuffer;
    }
}
LoupedeckProtocol.kSysexHeader = [0x00, 0x00, 0x66];
