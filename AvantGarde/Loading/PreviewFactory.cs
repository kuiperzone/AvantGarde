// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Avalonia.Media.Imaging;
using AvantGarde.Markup;
using AvantGarde.Projects;

namespace AvantGarde.Loading
{
    /// <summary>
    /// Creates instances of <see cref="PreviewPayload"/> using values derived from <see cref="LoadPayload"/>.
    /// </summary>
    public class PreviewFactory
    {
        private readonly static IReadOnlyList<string> ColorWheel = new string[]
            { "#3F976194", "#3F619764", "#3F617997", "#3F977F61" };

        private readonly PreviewPayload _source = new();
        private int _resendFlag;
        private int _wheelIndex;

        /// <summary>
        /// Constructor.
        /// </summary>
        public PreviewFactory(LoadPayload load)
        {
            Load = load;

            _source = new PreviewPayload();
            _source.Name = load.Name;
            _source.ItemKind = load.ItemKind;
            _source.IsProjectHeader = load.IsProjectHeader;

            if (load.Error != null)
            {
                _source.Error = new PreviewError(load.Error.Message);
            }

            if (!load.IsEmpty && load.FullPath != null)
            {
                try
                {
                    if (load.ItemKind.IsText() || load.ItemKind == PathKind.OtherFile)
                    {
                        // Always get the text if we can
                        _source.Text = new PathItem(load.FullPath, load.ItemKind).ReadAsText();
                    }

                    if (_source.Error == null)
                    {
                        if (load.ItemKind == PathKind.Xaml && !string.IsNullOrWhiteSpace(_source.Text))
                        {
                            var doc = XDocument.Parse(_source.Text);
                            var root = doc.Root ?? throw new XmlException("No root element");
                            _source.DesignWidth = ParseOrNaN(GetLocalAttribute(root, "DesignWidth")?.Value, "DesignWidth");
                            _source.DesignHeight = ParseOrNaN(GetLocalAttribute(root, "DesignHeight")?.Value, "DesignHeight");
                            _source.Width = GetDimension(root, "Width", "MinWidth", "MaxWidth");
                            _source.Height = GetDimension(root, "Height", "MinHeight", "MaxHeight");

                            Debug.WriteLine("Root Name: " + root.Name.LocalName);

                            if (root.Name.LocalName == "Window")
                            {
                                Debug.WriteLine("Is window");
                                _source.IsWindow = true;
                                _source.WindowTitle = root.Attribute("Title")?.Value;
                                _source.WindowCanResize = ((bool?)GetLocalAttribute(root, "CanResize")) ?? true;

                                if (load.Locator != null)
                                {
                                    var path = load.Locator.GetAssetFileName(root.Attribute("Icon")?.Value);

                                    if (path != null)
                                    {
                                        _source.WindowIcon = new Bitmap(path);
                                    }
                                }
                            }
                            else
                            if (root.Name.LocalName == "Application")
                            {
                                Debug.WriteLine("Is application");
                                _source.Error = new PreviewError("Application cannot be previewed");
                            }

                            if (_source.Error == null && load.Flags != LoadFlags.None && doc.Root != null)
                            {
                                Debug.WriteLine("ProcessXaml");
                                ProcessedXaml = ProcessXaml(root, doc, load);
                                _resendFlag = 1;
                            }
                        }
                        else
                        if (load.ItemKind == PathKind.Image)
                        {
                            _source.Source = new Bitmap(load.FullPath);
                            _source.Width = new ControlDimension(_source.Source.PixelSize.Width);
                            _source.Height = new ControlDimension(_source.Source.PixelSize.Height);
                        }
                    }
                }
                catch (XmlException e)
                {
                    _source.Error ??= new PreviewError(e.Message, e.LineNumber, e.LinePosition);
                }
                catch (Exception e)
                {
                    _source.Error ??= new PreviewError(e.Message);
                }
            }

        }

        /// <summary>
        /// Gets the input payload.
        /// </summary>
        public readonly LoadPayload Load;

        /// <summary>
        /// Gets the processed XAML text, if any. Can be null.
        /// </summary>
        public readonly string? ProcessedXaml;

        /// <summary>
        /// Gets whether the payload should be delivered to the view immediately, without sending to remote preview host.
        /// </summary>
        public bool IsImmediate
        {
            get
            {
                return Load.IsEmpty || Load.ItemKind != PathKind.Xaml || _source.Text == null ||
                    _source.Error != null || _source.Source != null;
            }
        }

        /// <summary>
        /// Gets the XAML text to be sent. If processed is true, the result will be <see cref="ProcessedXaml"/> if
        /// it is not null. Otherwise, it is the verbatim source text. The value is null if nothing should be sent
        /// to the remote host.
        /// </summary>
        public string? GetXaml(bool processed)
        {
            return processed && ProcessedXaml != null ? ProcessedXaml : _source.Text;
        }

        /// <summary>
        /// Gets a one-time only flag indicating that resend is necessary. The call is thread-safe and reset
        /// to false, so that an instance of this class returns true only once.
        /// </summary>
        public bool GetResendAndReset()
        {
            return Interlocked.Exchange(ref _resendFlag, 0) != 0;
        }

        /// <summary>
        /// Creates an instance of <see cref="PreviewPayload"/> derived from the basic information provided on
        /// construction. The result will need the preview image and error (if any).
        /// </summary>
        public PreviewPayload CreatePreview()
        {
            return _source.Clone();
        }

        private static ControlDimension GetDimension(XElement e, string v, string m, string x)
        {
            return new ControlDimension((double?)e.Attribute(v), (double?)e.Attribute(m), (double?)e.Attribute(x));
        }

        private static XAttribute? GetLocalAttribute(XElement e, string localName)
        {
            foreach (var item in e.Attributes())
            {
                if (item.Name.LocalName == localName)
                {
                    return item;
                }
            }

            return null;
        }

        private static double ParseOrNaN(string? value, string name)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                {
                    return result;
                }

                throw new ArgumentException("Invalid " + name + " " + value);
            }

            return double.NaN;
        }

        private string? ProcessXaml(XElement root, XDocument doc, LoadPayload payload)
        {
            ProcessElement(root, doc, payload);
            return doc.ToString();
        }

        private void ProcessElement(XElement e, XDocument doc, LoadPayload payload)
        {
            var local = e.Name.LocalName;
            var info = MarkupDictionary.GetMarkupInfo(local);

            if (info != null)
            {
                var opts = payload.Flags;

                if (opts.HasFlag(LoadFlags.DisableEvents))
                {
                    FilterEvents(e, info);
                }

                if (opts.HasFlag(LoadFlags.PrefetchAssets) && payload.Locator != null)
                {
                    FetchAssets(e, info, payload.Locator);
                }

                if (local == "Grid")
                {
                    if (opts.HasFlag(LoadFlags.GridLines) && e.Attribute("ShowGridLines") == null)
                    {
                        e.Add(new XAttribute("ShowGridLines", "True"));
                    }

                    if (opts.HasFlag(LoadFlags.GridColors) && e.Attribute("Background") == null)
                    {
                        int idx = _wheelIndex++ % ColorWheel.Count;
                        e.Add(new XAttribute("Background", ColorWheel[idx]));
                    }
                }

            }

            foreach (var item in new List<XElement>(e.Elements()))
            {
                ProcessElement(item, doc, payload);
            }
        }

        private static void FilterEvents(XElement e, MarkupInfo info)
        {
            foreach (var item in new List<XAttribute>(e.Attributes()))
            {
                if (!item.IsNamespaceDeclaration && string.IsNullOrEmpty(item.Name.Namespace.ToString()))
                {
                    if (info.Attributes.TryGetValue(item.Name.LocalName, out AttributeInfo? a) && a.IsEvent)
                    {
                        item.Remove();
                    }
                }
            }
        }

        private static void FetchAssets(XElement e, MarkupInfo info, AssetLocator locator)
        {
            foreach (var a in new List<XAttribute>(e.Attributes()))
            {
                if (info.Attributes.TryGetValue(a.Name.LocalName, out var attribInfo) &&
                    !attribInfo.IsEvent && AssetLocator.IsAsset(attribInfo))
                {
                    var path = locator.GetAssetFileName(a.Value);

                    if (path != null)
                    {
                        a.Value = "file://" + path;
                    }
                }
            }
        }
    }
}