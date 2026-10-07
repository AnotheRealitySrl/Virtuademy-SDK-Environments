namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// What a big screen shows when it loads its default media.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The field used to be typed <c>FileTypeExt</c>, the platform's file-catalogue enum, and the
    /// tooltip had to warn creators away from one of its five values ("do not use the Asset3D
    /// value"). A type whose inspector dropdown offers an invalid choice is the wrong type: this one
    /// offers exactly the three the application knows how to honour, and the warning is gone because
    /// the mistake is no longer expressible.
    /// </para>
    /// <para>
    /// <b>The numbers are load-bearing.</b> They match the <c>FileTypeExt</c> members they replace,
    /// because Unity serializes an enum field by its integer value: the Kit's own
    /// <c>BigScreen.prefab</c> carries <c>mediaType: 1</c>, and any creator scene that has set this
    /// field carries a number too. Renumbering would silently change what those screens show.
    /// </para>
    /// <para>
    /// <b>There is deliberately no member at 0.</b> <c>BigScreenNetworkBridge</c> initialises its own
    /// copy to <c>0</c> and spins on <c>mediaType == 0</c> to wait for the placeholder's data, so 0
    /// has to keep meaning "not set yet" rather than naming a kind of media.
    /// </para>
    /// </remarks>
    public enum EBigScreenMediaType
    {
        Video = 1,
        Documents = 2,
        Images = 3,
    }
}
