using vatsys;

namespace TagListPlugin;

public record QuickTagAddedMessage(QuickTag QuickTag);

public record QuickTagRemovedMessage(QuickTag QuickTag);

public record QuickTagLabelDataChangedMessage(QuickTag QuickTag);

public record QuickTagCflChangedMessage(QuickTag QuickTag);

public record RadarTrackAltitudeChangedMessage(RDP.RadarTrack RadarTrack);
