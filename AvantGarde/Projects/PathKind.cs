// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

namespace AvantGarde.Projects
{
    /// <summary>
    /// Project path kind. File kinds are based on extension.
    /// </summary>
    public enum PathKind
    {
        /// <summary>
        /// The path is a directory.
        /// </summary>
        Directory = 0,

        /// <summary>
        /// Use only for construction of file kinds. It causes the file to be identified as one of the kind below.
        /// </summary>
        AnyFile,

        /// <summary>
        /// Dotnet solution or project file.
        /// </summary>
        Solution,

        /// <summary>
        /// The ".cs" extension and related.
        /// </summary>
        CSharp,

        /// <summary>
        /// XML or related file, excluding XAML and AXAML.
        /// </summary>
        Xml,
        /// <param name="name"></param>
        /// <returns></returns>

        /// <summary>
        /// XAML or AXAML file.
        /// </summary>
        Xaml,

        /// <summary>
        /// Any support image file, jpg or png etc.
        /// </summary>
        Image,

        /// <summary>
        /// Any other recognised extension that is a text document.
        /// </summary>
        Document,

        /// <summary>
        /// A DLL or EXE.
        /// </summary>
        Assembly,

        /// <summary>
        /// Unknown or other file kind.
        /// </summary>
        OtherFile,
    }

    /// <summary>
    /// Extension methods.
    /// </summary>
    public static class PathKindExtension
    {
        /// <summary>
        /// Returns true if the kind is a readable text file.
        /// </summary>
        public static bool IsText(this PathKind kind)
        {
            return kind == PathKind.Solution || kind == PathKind.CSharp ||
                kind == PathKind.Xml || kind == PathKind.Xaml ||
                kind == PathKind.Document;
        }
    }
}