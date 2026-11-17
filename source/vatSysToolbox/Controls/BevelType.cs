namespace vatSysToolbox.Controls;

/// <summary>
/// How the edge of a <see cref="BeveledBorder"/> looks.
/// </summary>
public enum BevelType
{
    /// <summary>
    /// The content looks higher than the surrounding area.
    /// </summary>
    Raised,

    /// <summary>
    /// The content looks lower than the surrounding area.
    /// </summary>
    Sunken,

    /// <summary>
    /// A groove around the content: a raised edge outside a sunken edge. The border is two times
    /// <see cref="BeveledBorder.BorderThickness"/>.
    /// </summary>
    Outline
}