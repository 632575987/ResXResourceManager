﻿namespace tomenglertde.ResXManager.VSIX
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Xml.Linq;

    using JetBrains.Annotations;

    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell.Interop;

    using tomenglertde.ResXManager.View;

    internal static class DteExtensions
    {
        public static EnvDTE.Document TryGetDocument(this EnvDTE.ProjectItem projectItem)
        {
            try
            {
                return projectItem?.Document;
            }
            catch
            {
                return null;
            }
        }

        public static XDocument TryGetContent([NotNull] this EnvDTE.ProjectItem projectItem)
        {
            Contract.Requires(projectItem != null);

            try
            {
                return !projectItem.IsOpen ? null : TryGetContent(projectItem.TryGetDocument());
            }
            catch (Exception)
            {
                return null;
            }
        }

        [NotNull]
        public static ICollection<VSITEMSELECTION> GetSelectedProjectItems([NotNull] this IVsMonitorSelection monitorSelection)
        {
            Contract.Requires(monitorSelection != null);
            Contract.Ensures(Contract.Result<ICollection<VSITEMSELECTION>>() != null);

            var hierarchyPtr = IntPtr.Zero;
            var selectionContainerPtr = IntPtr.Zero;

            try
            {
                var hr = monitorSelection.GetCurrentSelection(out hierarchyPtr, out var itemId, out var multiItemSelect, out selectionContainerPtr);

                if (ErrorHandler.Failed(hr))
                    return new VSITEMSELECTION[0];

                if ((itemId == VSConstants.VSITEMID_SELECTION) && (multiItemSelect != null))
                {
                    multiItemSelect.GetSelectionInfo(out var cItems, out var info);
                    var items = new VSITEMSELECTION[cItems];
                    multiItemSelect.GetSelectedItems(0, cItems, items);
                    return items;
                }

                if ((hierarchyPtr == IntPtr.Zero) || (itemId == VSConstants.VSITEMID_ROOT))
                    return new VSITEMSELECTION[0];

                return new[]
                {
                    new VSITEMSELECTION
                    {
                        pHier = Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy,
                        itemid = itemId
                    }
                };
            }
            finally
            {
                if (selectionContainerPtr != IntPtr.Zero)
                    Marshal.Release(selectionContainerPtr);

                if (hierarchyPtr != IntPtr.Zero)
                    Marshal.Release(hierarchyPtr);
            }
        }

        public static string GetMkDocument(this VSITEMSELECTION selection)
        {
            try
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                var vsProject = selection.pHier as IVsProject;

                if (vsProject == null)
                    return null;

                vsProject.GetMkDocument(selection.itemid, out var itemFullPath);

                return itemFullPath;
            }
            catch (Exception)
            {
                return null;
            }
        }

        [ContractVerification(false)]
        private static XDocument TryGetContent(EnvDTE.Document document)
        {
            try
            {
                var textDocument = (EnvDTE.TextDocument)document?.Object(@"TextDocument");
                var text = textDocument?.CreateEditPoint().GetText(textDocument.EndPoint);

                return text == null ? null : XDocument.Parse(text);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool TrySetContent([NotNull] this EnvDTE.ProjectItem projectItem, [NotNull] XDocument value)
        {
            Contract.Requires(projectItem != null);
            Contract.Requires(value != null);

            return projectItem.IsOpen && TrySetContent(projectItem.TryGetDocument(), value);
        }

        [ContractVerification(false)]
        private static bool TrySetContent(EnvDTE.Document document, XDocument value)
        {
            try
            {
                var textDocument = (EnvDTE.TextDocument)document?.Object(@"TextDocument");
                if (textDocument == null)
                    return false;

                var text = value.Declaration + Environment.NewLine + value;

                textDocument.CreateEditPoint().ReplaceText(textDocument.EndPoint, text, 0);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [NotNull]
        public static IEnumerable<EnvDTE.ProjectItem> Children(this EnvDTE.ProjectItem projectItem)
        {
            Contract.Ensures(Contract.Result<IEnumerable<EnvDTE.ProjectItem>>() != null);

            if (projectItem == null)
                return Enumerable.Empty<EnvDTE.ProjectItem>();

            var projectItems = projectItem.ProjectItems;
            if (projectItems == null)
                return Enumerable.Empty<EnvDTE.ProjectItem>();

            return projectItems.OfType<EnvDTE.ProjectItem>();
        }

        [NotNull]
        public static IEnumerable<EnvDTE.ProjectItem> Descendants(this EnvDTE.ProjectItem projectItem)
        {
            Contract.Ensures(Contract.Result<IEnumerable<EnvDTE.ProjectItem>>() != null);

            if (projectItem == null)
                return Enumerable.Empty<EnvDTE.ProjectItem>();

            var projectItems = projectItem.ProjectItems;
            if (projectItems == null)
                return Enumerable.Empty<EnvDTE.ProjectItem>();

            return projectItem.ProjectItems.OfType<EnvDTE.ProjectItem>().SelectMany(p => p.DescendantsAndSelf());
        }

        [NotNull]
        public static IEnumerable<EnvDTE.ProjectItem> DescendantsAndSelf(this EnvDTE.ProjectItem projectItem)
        {
            Contract.Ensures(Contract.Result<IEnumerable<EnvDTE.ProjectItem>>() != null);

            if (projectItem == null)
                yield break;

            yield return projectItem;

            foreach (var item in projectItem.Descendants())
            {
                yield return item;
            }
        }

        public static void SetProperty([NotNull] this EnvDTE.ProjectItem projectItem, [NotNull] string propertyName, object value)
        {
            Contract.Requires(projectItem != null);
            Contract.Requires(propertyName != null);

            var item = projectItem.Properties?.Item(propertyName);
            if (item != null)
                item.Value = value;
        }

        public static object GetProperty([NotNull] this EnvDTE.ProjectItem projectItem, [NotNull] string propertyName)
        {
            Contract.Requires(projectItem != null);
            Contract.Requires(propertyName != null);

            try
            {
                return projectItem.Properties?.OfType<EnvDTE.Property>()
                    .Where(p => p.Name == propertyName)
                    .Select(p => p.Value)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }


        public static void RunCustomTool([NotNull] this EnvDTE.ProjectItem projectItem)
        {
            Contract.Requires(projectItem != null);

            try
            {
                var vsProjectItem = projectItem.Object as VSLangProj.VSProjectItem;

                vsProjectItem?.RunCustomTool();
            }
            catch
            {
                // ignore
            }
        }

        public static void SetCustomTool([NotNull] this EnvDTE.ProjectItem projectItem, string value)
        {
            Contract.Requires(projectItem != null);

            SetProperty(projectItem, @"CustomTool", value);
        }

        public static string GetCustomTool([NotNull] this EnvDTE.ProjectItem projectItem)
        {
            Contract.Requires(projectItem != null);

            return GetProperty(projectItem, @"CustomTool") as string;
        }

        public static EnvDTE.ProjectItem AddFromFile([NotNull] this EnvDTE.ProjectItem projectItem, string fileName)
        {
            Contract.Requires(projectItem != null);

            var projectItems = projectItem.ProjectItems;

            try
            {
                return projectItems?.AddFromFile(fileName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static EnvDTE.ProjectItem AddFromFile([NotNull] this EnvDTE.Project project, string fileName)
        {
            Contract.Requires(project != null);

            var projectItems = project.ProjectItems;

            try
            {
                return projectItems?.AddFromFile(fileName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static void SetFontSize([NotNull] this EnvDTE.DTE dte, [NotNull] DependencyObject view)
        {
            Contract.Requires(dte != null);
            Contract.Requires(view != null);

            const string CATEGORY_FONTS_AND_COLORS = "FontsAndColors";
            const string PAGE_TEXT_EDITOR = "TextEditor";
            const string PROPERTY_FONT_SIZE = "FontSize";

            try
            {
                var fontSizeObject = dte.Properties[CATEGORY_FONTS_AND_COLORS, PAGE_TEXT_EDITOR]?.Item(PROPERTY_FONT_SIZE)?.Value ?? 0.0;

                var fontSize = Convert.ToDouble(fontSizeObject, CultureInfo.InvariantCulture);

                if (fontSize > 1)
                {
                    // Default in VS is 10, but looks like 12 in WPF
                    view.SetValue(Appearance.TextFontSizeProperty, fontSize * 1.2);
                }
            }
            catch
            {
                // ignored
            }
        }

        public static string TryGetFileName([NotNull] this EnvDTE.ProjectItem projectItem)
        {
            Contract.Requires(projectItem != null);

            try
            {
                if (string.Equals(projectItem.Kind, ItemKind.PhysicalFile, StringComparison.OrdinalIgnoreCase))
                {
                    // some items report a file count > 0 but don't return a file name!
                    return projectItem.FileNames[0];
                }
            }
            catch (ArgumentException)
            {
            }

            return null;
        }

    }
}
