// The surfaces a tooth can be viewed from. A region can belong to several.
// Names must match the ViewMarkers children (Buccal, Lingual, ...).
[System.Flags]
public enum ToothView
{
    None     = 0,
    Buccal   = 1 << 0,
    Lingual  = 1 << 1,
    Mesial   = 1 << 2,
    Distal   = 1 << 3,
    Occlusal = 1 << 4
}