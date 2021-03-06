﻿#region Imports

using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using CrmCodeGenerator.VSPackage.Dialogs;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.Model;
using CrmCodeGenerator.VSPackage.T4;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Designer.Interfaces;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextTemplating;
using Microsoft.VisualStudio.TextTemplating.VSHost;
//using VSLangProj80;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

#endregion

namespace CrmCodeGenerator.VSPackage
{
	public static class vsContextGuids
	{
		public const string vsContextGuidVCSProject = "{FAE04EC1-301F-11D3-BF4B-00C04F79EFBC}";
		public const string vsContextGuidVCSEditor = "{694DD9B6-B865-4C5B-AD85-86356E9C88DC}";
		public const string vsContextGuidVBProject = "{164B10B9-B200-11D0-8C61-00A0C91E29D5}";
		public const string vsContextGuidVBEditor = "{E34ACDC0-BAAE-11D0-88BF-00A0C9110049}";
		public const string vsContextGuidVJSProject = "{E6FDF8B0-F3D1-11D4-8576-0002A516ECE8}";
		public const string vsContextGuidVJSEditor = "{E6FDF88A-F3D1-11D4-8576-0002A516ECE8}";
	}


	// http://blogs.msdn.com/b/vsx/archive/2013/11/27/building-a-vsix-deployable-single-file-generator.aspx
	[ComVisible(true)]
	[Guid(GuidList.guidCrmCodeGenerator_SimpleGenerator)]
	[ProvideObject(typeof (CrmCodeGenerator2011))]
	[CodeGeneratorRegistration(typeof (CrmCodeGenerator2011), "CrmCodeGenerator2011",
		vsContextGuids.vsContextGuidVCSProject, GeneratesDesignTimeSource = true)]
	[CodeGeneratorRegistration(typeof (CrmCodeGenerator2011), "CrmCodeGenerator2011", vsContextGuids.vsContextGuidVBProject,
		GeneratesDesignTimeSource = true)]
	public class CrmCodeGenerator2011 : IVsSingleFileGenerator, IObjectWithSite, IDisposable
	{
		private object site = null;
		private CodeDomProvider codeDomProvider = null;
		private ServiceProvider serviceProvider = null;
		private string extension = null;
		private Context context = null;

		private CodeDomProvider CodeProvider
		{
			get
			{
				if (codeDomProvider == null)
				{
					var provider = (IVSMDCodeDomProvider) SiteServiceProvider.GetService(typeof (IVSMDCodeDomProvider).GUID);
					if (provider != null)
					{
						codeDomProvider = (CodeDomProvider) provider.CodeDomProvider;
					}
				}
				return codeDomProvider;
			}
		}

		private ServiceProvider SiteServiceProvider
		{
			get
			{
				if (serviceProvider == null)
				{
					var oleServiceProvider = site as IOleServiceProvider;
					serviceProvider = new ServiceProvider(oleServiceProvider);
				}
				return serviceProvider;
			}
		}

		public void Dispose()
		{
			if (codeDomProvider != null)
			{
				codeDomProvider.Dispose();
				codeDomProvider = null;
			}
			if (serviceProvider != null)
			{
				serviceProvider.Dispose();
				serviceProvider = null;
			}
		}

		#region IVsSingleFileGenerator

		public int DefaultExtension(out string pbstrDefaultExtension)
		{
			pbstrDefaultExtension = "." + CodeProvider.FileExtension;
			if (extension != null)
			{
				pbstrDefaultExtension = extension;
			}
			return VSConstants.S_OK;
		}

		public int Generate(string wszInputFilePath, string bstrInputFileContents, string wszDefaultNamespace,
			IntPtr[] rgbOutputFileContents, out uint pcbOutput, IVsGeneratorProgress pGenerateProgress)
		{
			if (bstrInputFileContents == null)
			{
				throw new ArgumentException(bstrInputFileContents);
			}

			Status.Clear();

			var originalFile = GetOriginalFile(wszInputFilePath);

			var dte = Package.GetGlobalService(typeof (SDTE)) as DTE2;

			Configuration.FileName = Path.GetFileNameWithoutExtension(wszInputFilePath);

			//var settingsNew = Configuration.LoadConfigs().GetSelectedSettings();

			//context = settingsNew.Context;

			// use the 'cached' button instead
			var refresh = true;//context == null || PromptToRefreshEntities();

			if (refresh)
			{
				var m = new Login(dte);
				m.ShowModal();
				context = m.Context;
				m = null;
			}

			if (context == null)
			{
				// TODO  pGenerateProgress.GeneratorError(1, (uint)1, "Code generation for CRM Template aborted", uint.MaxValue, uint.MaxValue);
				if (Configuration.FileName != null)
				{
					var fileName = wszInputFilePath.Replace(".tt", ".cs");
					if (File.Exists(fileName))
						// http://social.msdn.microsoft.com/Forums/vstudio/en-US/d8d72da3-ddb9-4811-b5da-2a167bbcffed/ivssinglefilegenerator-cancel-code-generation
						// I don't think a login failure would be considered a invalid model, so we'll restore what was there
					{
						SaveOutputContent(
							rgbOutputFileContents, out pcbOutput, File.ReadAllText(fileName));
					}
					else
					{
						SaveOutputContent(rgbOutputFileContents, out pcbOutput, "");
					}
				}
				else
				{
					SaveOutputContent(rgbOutputFileContents, out pcbOutput, "");
				}
				return VSConstants.S_OK;
			}

			Status.Update("Generating code from template ... ", false);

			var t4 = Package.GetGlobalService(typeof (STextTemplating)) as ITextTemplating;
			var sessionHost = t4 as ITextTemplatingSessionHost;

			context.Namespace = wszDefaultNamespace;
			context.FileName = Configuration.FileName;
			sessionHost.Session = sessionHost.CreateSession();
			sessionHost.Session["Context"] = context;

			var cb = new Callback();
			t4.BeginErrorSession();
			var content = t4.ProcessTemplate(wszInputFilePath, bstrInputFileContents, cb);
			t4.EndErrorSession();

			// Append any error/warning to output window
			foreach (var err in cb.ErrorMessages)
			{
				// The templating system (eg t4.ProcessTemplate) will automatically add error/warning to the ErrorList 
				Status.Update(string.Format("[{0}] {1} {2}, {3}",
					(err.Warning ? "WARN" : "ERROR"), err.Message, err.Line, err.Column));
			}

			// If there was an output directive in the TemplateFile, then cb.SetFileExtension() will have been called.
			if (!string.IsNullOrWhiteSpace(cb.FileExtension))
			{
				extension = cb.FileExtension;
			}

			Status.Update("done!");

			Status.Update("Writing code to disk ... ", false);

			SaveOutputContent(rgbOutputFileContents, out pcbOutput, content);

			Status.Update("done!");

			return VSConstants.S_OK;
		}

		private static void SaveOutputContent(IntPtr[] rgbOutputFileContents, out uint pcbOutput, string content)
		{
			var bytes = Encoding.UTF8.GetBytes(content);

			if (bytes == null)
			{
				rgbOutputFileContents[0] = IntPtr.Zero;
				pcbOutput = 0;
			}
			else
			{
				rgbOutputFileContents[0] = Marshal.AllocCoTaskMem(bytes.Length);
				Marshal.Copy(bytes, 0, rgbOutputFileContents[0], bytes.Length);
				pcbOutput = (uint) bytes.Length;
			}
		}

		private static string GetOriginalFile(string wszInputFilePath)
		{
			var dte = Package.GetGlobalService(typeof (SDTE)) as DTE;
			var project = dte.GetSelectedProject();
			var relFile = DteHelper.MakeRelative(wszInputFilePath, project.GetProjectDirectory());
			var pi = project.GetProjectItem(relFile);
			foreach (ProjectItem item in pi.ProjectItems)
			{
				// It possible for the project item to be corrupt. (ie project has a reference to a file, but the file is gone).  
				//  when this happens the item will have a NULL document.
				if (item.Document != null)
				{
					return item.Document.FullName;
				}
			}
			return null;
		}

		private static bool PromptToRefreshEntities()
		{
			var results = VsShellUtilities.ShowMessageBox(ServiceProvider.GlobalProvider,
				"Do you want to refresh the CRM entities from the Server?\n" +
				" If 'no', the generator will use the cached entities of the latest run.",
				"Refresh", OLEMSGICON.OLEMSGICON_QUERY,
				OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

			return results == 6;
		}

		#endregion IVsSingleFileGenerator

		#region IObjectWithSite

		public void GetSite(ref Guid riid, out IntPtr ppvSite)
		{
			if (site == null)
			{
				Marshal.ThrowExceptionForHR(VSConstants.E_NOINTERFACE);
			}

			// Query for the interface using the site object initially passed to the generator 
			var punk = Marshal.GetIUnknownForObject(site);
			var hr = Marshal.QueryInterface(punk, ref riid, out ppvSite);
			Marshal.Release(punk);
			ErrorHandler.ThrowOnFailure(hr);
		}

		public void SetSite(object pUnkSite)
		{
			// Save away the site object for later use 
			site = pUnkSite;

			// These are initialized on demand via our private CodeProvider and SiteServiceProvider properties 
			codeDomProvider = null;
			serviceProvider = null;
		}

		#endregion IObjectWithSite
	}
}
