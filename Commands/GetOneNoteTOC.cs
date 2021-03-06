#region Microsoft Community License
/*****
Microsoft Community License (Ms-CL)
Published: October 12, 2006

   This license governs  use of the  accompanying software. If you use
   the  software, you accept this  license. If you  do  not accept the
   license, do not use the software.

1. Definitions

   The terms "reproduce,"    "reproduction," "derivative works,"   and
   "distribution" have  the same meaning here  as under U.S. copyright
   law.

   A  "contribution" is the  original  software, or  any additions  or
   changes to the software.

   A "contributor"  is any  person  that distributes  its contribution
   under this license.

   "Licensed  patents" are  a contributor's  patent  claims that  read
   directly on its contribution.

2. Grant of Rights

   (A) Copyright   Grant-  Subject to  the   terms  of  this  license,
   including the license conditions and limitations in section 3, each
   contributor grants   you a  non-exclusive,  worldwide, royalty-free
   copyright license to reproduce its contribution, prepare derivative
   works of its  contribution, and distribute  its contribution or any
   derivative works that you create.

   (B) Patent Grant-  Subject to the terms  of this license, including
   the   license   conditions and   limitations   in  section  3, each
   contributor grants you   a non-exclusive, worldwide,   royalty-free
   license under its licensed  patents to make,  have made, use, sell,
   offer   for   sale,  import,  and/or   otherwise   dispose  of  its
   contribution   in  the  software   or   derivative  works  of   the
   contribution in the software.

3. Conditions and Limitations

   (A) Reciprocal  Grants- For any  file you distribute  that contains
   code from the software (in source code  or binary format), you must
   provide recipients the source code  to that file  along with a copy
   of this  license,  which license  will  govern that  file.  You may
   license other  files that are  entirely  your own  work and  do not
   contain code from the software under any terms you choose.

   (B) No Trademark License- This license does not grant you rights to
   use any contributors' name, logo, or trademarks.

   (C)  If you  bring  a patent claim    against any contributor  over
   patents that you claim  are infringed by  the software, your patent
   license from such contributor to the software ends automatically.

   (D) If you distribute any portion of the  software, you must retain
   all copyright, patent, trademark,  and attribution notices that are
   present in the software.

   (E) If  you distribute any  portion of the  software in source code
   form, you may do so only under this license by including a complete
   copy of this license with your  distribution. If you distribute any
   portion  of the software in  compiled or object  code form, you may
   only do so under a license that complies with this license.

   (F) The  software is licensed  "as-is." You bear  the risk of using
   it.  The contributors  give no  express  warranties, guarantees  or
   conditions. You   may have additional  consumer  rights  under your
   local laws   which  this license  cannot   change. To   the  extent
   permitted under  your local  laws,   the contributors  exclude  the
   implied warranties of   merchantability, fitness for  a  particular
   purpose and non-infringement.


*****/
#endregion

using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using System.Xml;
using Microsoft.Office.Interop.OneNote;

namespace Microsoft.Office.OneNote.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Get, "OneNoteTOC", SupportsShouldProcess = true)]
    public class GetOneNoteTOC : PSCmdlet
    {

        #region Parameters

        private string path;

        [Parameter(Position=0)]
        public string Path
        {
            get { return path; }
            set { path = value; }
        }

        private string _tocSectionPath;

        [Parameter(HelpMessage="Optional: Path to the section that will hold the new TOC page. Defaults to the section the TOC is created for.")]
        public string TocSectionPath
        {
            get { return _tocSectionPath; }
            set { _tocSectionPath = value; }
        }
	

        private HierarchyScope _scope = HierarchyScope.hsChildren;

        [Parameter(HelpMessage="Determines how deep to build the TOC.")]
        public HierarchyScope Scope
        {
            get { return _scope; }
            set { _scope = value; }
        }

        private bool _passThru;

        [Parameter(HelpMessage="If set, then the TOC page XML is written to the pipeline.")]
        public SwitchParameter PassThru
        {
            get { return _passThru; }
            set { _passThru = value; }
        }
        #endregion

#region Private data

        private ApplicationClass _application;

        /// <summary>
        /// This are the valid OneNote types for which we can get a TOC.
        /// </summary>
        private List<string> _validNodeTypes = new List<string>( );

        /// <summary>
        /// Used for xpath queries against the OneNote hierarchy. 
        /// </summary>
        private XmlNamespaceManager nsmgr;

#endregion

        protected override void BeginProcessing( )
        {
            base.BeginProcessing( );
            _application = new ApplicationClass( );
            
            //
            //  Initialize the valid node types.
            //
            
            _validNodeTypes.Add("Notebook");
            _validNodeTypes.Add("Section");
            _validNodeTypes.Add("SectionGroup");
        }


        protected override void ProcessRecord( )
        {
            if (_scope == HierarchyScope.hsSelf)
            {
                //
                //  Nothing to do in this case.
                //

                WriteVerbose("No TOC can be created for the hsSelf scope.");
                return;
            }
            //
            //  Determine if we're supposed to override the section ID in which we create the TOC.
            //

            string overrideSectionId = null;
            if (!String.IsNullOrEmpty(_tocSectionPath))
            {
                List<string> tocSections = new List<string>( );
                Utilities.GetOneNoteIdsForPsPath(this, _tocSectionPath, tocSections);
                if (tocSections.Count >= 1)
                {
                    overrideSectionId = tocSections[0];
                    WriteVerbose("New TOC pages will go in Section " + overrideSectionId);
                }
            }
            List<string> ids = new List<string>( );
            Utilities.GetOneNoteIdsForPsPath(this, path, ids);
            foreach (string id in ids)
            {
                string hierarchy;
                _application.GetHierarchy(id, HierarchyScope.hsPages, out hierarchy);
                XmlDocument hierarchyDoc = new XmlDocument( );
                hierarchyDoc.LoadXml(hierarchy);
                nsmgr = new XmlNamespaceManager(hierarchyDoc.NameTable);
                nsmgr.AddNamespace("one", Microsoft.Office.OneNote.PowerShell.Provider.OneNoteXml.OneNoteSchema);

                //
                //  We must have a Notebook, Section, or SectionGroup.
                //

                WriteDebug("Found " + hierarchyDoc.DocumentElement.LocalName);
                if (!_validNodeTypes.Contains(hierarchyDoc.DocumentElement.LocalName))
                {
                    string errorMessage = "You must specify the path to a Notebook, Section, or Section Group. '{0}' is a {1}";
                    WriteError(new ErrorRecord(new ArgumentException(String.Format(errorMessage, id, hierarchyDoc.DocumentElement.LocalName)),
                        "ArgumentException",
                        ErrorCategory.InvalidArgument,
                        id)
                    );
                    continue;
                }
                if (hierarchyDoc.DocumentElement.ChildNodes.Count == 0)
                {
                    WriteError(new ErrorRecord(new ArgumentException("No sections exist to put into a TOC."),
                        "ArgumentException",
                        ErrorCategory.InvalidArgument,
                        id)
                    );
                    continue;
                }

                //
                //  Build up the TOC as HTML, then build a OneNote XML envelope around it.
                //

                System.Text.StringBuilder tocHtml = new StringBuilder( );
                tocHtml.Append("<html><head></head><body><ul>\n");
                foreach (XmlElement e in hierarchyDoc.DocumentElement.ChildNodes)
                {
                    addElementHtml(e, tocHtml, "  ");
                }
                tocHtml.Append("</ul></body></html>\n");

                string sectionId;
                if (!string.IsNullOrEmpty(overrideSectionId))
                {
                    sectionId = overrideSectionId;
                } else
                {
                    sectionId = id;
                }
                Microsoft.Office.OneNote.PowerShell.Provider.OneNoteXml pageXml = new Microsoft.Office.OneNote.PowerShell.Provider.OneNoteXml();
                XmlElement pageElement = pageXml.CreatePage("Table of Contents", null);
                pageXml.Document.AppendChild(pageElement);
                XmlElement htmlData = pageXml.FindOrCreate(pageElement,
                    new string[] { "Outline", "OEChildren", "HTMLBlock", "Data" });
                htmlData.AppendChild(pageXml.Document.CreateCDataSection(tocHtml.ToString( )));

                if (ShouldProcess("Add TOC page?"))
                {
                    try 
                    {
                        string newPageId;
                        _application.CreateNewPage(sectionId, out newPageId, NewPageStyle.npsBlankPageWithTitle);
                        XmlAttribute idAttribute = pageXml.Document.CreateAttribute("ID");
                        idAttribute.Value = newPageId;
                        pageElement.Attributes.Append(idAttribute);
                        _application.UpdatePageContent(Utilities.PrettyPrintXml(pageXml.Document), DateTime.MinValue);
                    } catch (Exception e)
                    {
                        WriteError(new ErrorRecord(e, "PageImport", ErrorCategory.WriteError, pageXml.Document));
                    }
                }
                if (_passThru)
                {
                    WriteObject(Utilities.PrettyPrintXml(pageXml.Document));
                }
            }
        }

        private void addElementHtml(XmlElement e, StringBuilder tocHtml, string indent)
        {
            if ((e.LocalName != "Notebook") &&
                (e.LocalName != "SectionGroup") &&
                (e.LocalName != "Section") &&
                (e.LocalName != "Page"))
            {

                WriteDebug("Looking at " + e.LocalName + "; skipping.");
                return;
            }
            WriteDebug("Adding element to TOC    " + e.LocalName);
            string id = e.Attributes["ID"].Value;
            string name = e.Attributes["name"].Value;
            WriteDebug("Element name: " + name);

            string link = null;

            if (e.LocalName == "Section")
            {
                XmlNode firstPage = e.SelectSingleNode("one:Page", nsmgr);
                if (firstPage != null)
                {
                    string pageId = firstPage.Attributes["ID"].Value;
                    _application.GetHyperlinkToObject(pageId, null, out link);
                    tocHtml.Append(indent + "<li><a href='" + link + "'>" + name + "</a></li>\n");
                } else
                {
                    tocHtml.Append(indent + "<li>" + name + "</li>");
                }
            } else if (e.LocalName == "Page")
            {
                _application.GetHyperlinkToObject(id, null, out link);
                tocHtml.Append(indent + "<li><a href='" + link + "'>" + name + "</a></li>\n");
            } else
            {
                tocHtml.Append(indent + "<li>" + name + "</li>");
            }
            if ((_scope == HierarchyScope.hsSections) && (e.LocalName == "Section"))
            {
                //
                //  We go no deeper in the OneNote hierarchy.
                //
                
                return;
            }
            if ((_scope != HierarchyScope.hsChildren) && (e.ChildNodes.Count > 0))
            {
                tocHtml.Append(indent + "<ul>\n");
                foreach (XmlElement child in e.ChildNodes)
                {
                    addElementHtml(child, tocHtml, indent + "  ");
                }
                tocHtml.Append(indent + "</ul>\n");
            }
        }
    }
}
