using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System;
using System.Reflection;
using System.Xml.Serialization;
using System.Security.Cryptography.X509Certificates;

namespace Igor
{
	public interface IIgorCore
	{
		List<string> GetEnabledModuleNames();

		void RunJobInst();
	}

	public partial class IgorUtils
	{
		public static List<Type> GetTypesWith<TAttribute>(bool bInherit) where TAttribute : Attribute
		{
			List<Type> AllTypes = new List<Type>();

			Assembly[] Assemblies = AppDomain.CurrentDomain.GetAssemblies();

			foreach(Assembly CurrentAssembly in Assemblies)
			{
				foreach(Type CurrentType in CurrentAssembly.GetTypes())
				{
					if(CurrentType.IsDefined(typeof(TAttribute), bInherit))
					{
						AllTypes.Add(CurrentType);
					}
				}
			}

			return AllTypes;
		}

		public static List<Type> GetTypesInheritFrom<InheritType>(bool bExcludeTemplateType = true)
		{
			List<Type> AllTypes = new List<Type>();

			Assembly[] Assemblies = AppDomain.CurrentDomain.GetAssemblies();

			foreach(Assembly CurrentAssembly in Assemblies)
			{
				foreach(Type CurrentType in CurrentAssembly.GetTypes())
				{
					if(typeof(InheritType).IsAssignableFrom(CurrentType) && (!bExcludeTemplateType || typeof(InheritType) != CurrentType))
					{
						AllTypes.Add(CurrentType);
					}
				}
			}

			return AllTypes;
		}

		public static void DeleteFile(string TargetFile)
		{
			if(File.Exists(TargetFile))
			{
		        File.SetAttributes(TargetFile, System.IO.FileAttributes.Normal);
		        File.Delete(TargetFile);
		    }
		}

		public static void DeleteDirectory(string targetDir)
		{
			if(!Directory.Exists(targetDir))
			{
				return;
			}

		    System.IO.File.SetAttributes(targetDir, System.IO.FileAttributes.Normal);
		
		    string[] files = System.IO.Directory.GetFiles(targetDir);
		    string[] dirs = System.IO.Directory.GetDirectories(targetDir);
		
		    foreach (string file in files)
		    {
		        System.IO.File.SetAttributes(file, System.IO.FileAttributes.Normal);
		        System.IO.File.Delete(file);
		    }
		
		    foreach (string dir in dirs)
		    {
		        DeleteDirectory(dir);
		    }
		
		    System.IO.Directory.Delete(targetDir, false);
		}

        public static string GetLocalFileFromModuleFilename(string Filename)
		{
			string LocalFileName = Filename;

			if(LocalFileName.StartsWith("("))
			{
				LocalFileName = LocalFileName.Substring(LocalFileName.IndexOf(")") + 1);
			}

			LocalFileName = LocalFileName.Replace("[", "");
			LocalFileName = LocalFileName.Replace("]", "");

			return LocalFileName;
		}

		public static string GetRemoteFileFromModuleFilename(string RootPath, string Filename, ref bool bIsRemote)
		{
			string RemoteFileName = Filename;

			bIsRemote = false;

			if(RemoteFileName.StartsWith("("))
			{
				RemoteFileName = RemoteFileName.Replace("(", "");
				RemoteFileName = RemoteFileName.Replace(")", "");

				bIsRemote = true;
			}
			else
			{
				if(RemoteFileName.StartsWith("."))
				{
					RemoteFileName = RemoteFileName.Substring(RemoteFileName.LastIndexOf("/") + 1);
				}

				RemoteFileName = RootPath + RemoteFileName;
			}

			if(RemoteFileName.Contains("["))
			{
				int IndexStart = RemoteFileName.IndexOf("[");
				int IndexEnd = RemoteFileName.LastIndexOf("]") + 1;

				RemoteFileName = RemoteFileName.Replace(RemoteFileName.Substring(IndexStart, IndexEnd - IndexStart), "");
			}

			return RemoteFileName;
		}

		public static string DownloadFileForUpdate(string RelativePath, string AbsolutePath = "")
		{
			string DestFilePath = Path.Combine(IgorUpdater.TempLocalDirectory, RelativePath);
			
			try
			{
				if(File.Exists(DestFilePath))
				{
					IgorUtils.DeleteFile(DestFilePath);
				}

				if(!Directory.Exists(Path.GetDirectoryName(DestFilePath)))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(DestFilePath));
				}

				if((IgorUpdater.bLocalDownload || IgorUpdater.bDontUpdate) && AbsolutePath == "")
				{
					string ParentDirectory = Directory.GetCurrentDirectory();
					string NewLocalPrefix = IgorUpdater.LocalPrefix;

					while(NewLocalPrefix.StartsWith(".."))
					{
						ParentDirectory = Directory.GetParent(ParentDirectory).ToString();

						NewLocalPrefix = NewLocalPrefix.Substring(3);
					}

					NewLocalPrefix = Path.Combine(ParentDirectory, NewLocalPrefix);
                    string LocalFilePath = Path.Combine(NewLocalPrefix, RelativePath);

                    if(File.Exists(LocalFilePath))
					    File.Copy(LocalFilePath, DestFilePath);
				}
				else
				{
                    string PathToSource = string.Empty;
                    if(AbsolutePath == "")
					{
						PathToSource = IgorUpdater.RemotePrefix + RelativePath;
					}
					else
					{
						PathToSource = AbsolutePath;
                    }
                    
				    WWW www = new WWW(PathToSource);
                    while(!www.isDone)
                    { }

                    if(www.error != null && www.error != "")
                    {
                    	Debug.LogError("Downloading " + PathToSource + " failed.  Error is \"" + www.error + "\"");
                    }
                    else
                    {
					    File.WriteAllText(DestFilePath, www.text);
                    }

                    www.Dispose();
				}
			}
			catch(Exception e)
			{
				Debug.LogError("Failed to download file " + RelativePath + " with error " + e.ToString());
			}

			return DestFilePath;
		}

        public static string ClearParam(string AllParams, string Param)
        {
            string Query = string.Empty;
            string StringValue = "";
            bool bStringParam = false;

            if(IsBoolParamSet(AllParams, Param))
            {
                Query = "--" + Param + " ";
            }
            else
            if(IsStringParamSet(AllParams, Param))
            {
            	bStringParam = true;
                StringValue = GetStringParam(AllParams, Param);
                Query = "--" + Param + "=" + "\"" + StringValue + "\"";
            }

            int StartParamIndex = AllParams.IndexOf(Query);

            if(StartParamIndex == -1 && !bStringParam)
            {
            	Query = "--" + Param;
            	StartParamIndex = AllParams.IndexOf(Query);

            	if((StartParamIndex + Query.Length) != AllParams.Length)
            	{
            		StartParamIndex = -1;
            	}
            }

            if(StartParamIndex == -1 && bStringParam)
            {
            	Query = "--" + Param + "=" + StringValue;
            	StartParamIndex = AllParams.IndexOf(Query);
            }

            if(StartParamIndex != -1)
            {
	            string TrimTarget = AllParams.Substring(0, StartParamIndex);
	            string TrimmedTarget = TrimTarget.TrimEnd(new char[] { ' ' });
	            int Difference = TrimTarget.Length - TrimmedTarget.Length;
	            string ReplaceText = AllParams.Substring(StartParamIndex - Difference, Query.Length + Difference);
	            
	            if(!string.IsNullOrEmpty(ReplaceText))
	            {
	                AllParams = AllParams.Replace(ReplaceText, string.Empty);
	            }
	        }

            return AllParams;
        }

	 	public static bool IsBoolParamSet(string AllParams, string BoolParam)
	 	{
            string ContainsQuery = "--" + BoolParam + " ";
            bool bHasFlag = AllParams.Contains(ContainsQuery);

            if(!bHasFlag)
            {
            	ContainsQuery = "--" + BoolParam;
            	int StartParamIndex = AllParams.IndexOf(ContainsQuery);

            	if((StartParamIndex + ContainsQuery.Length) == AllParams.Length)
            	{
            		bHasFlag = true;
            	}
            }

 			if(bHasFlag)
 			{
                string Substring = AllParams.Substring(AllParams.IndexOf(ContainsQuery) + ContainsQuery.Length);
                if(Substring.Length > 0)
                {
                    return Substring[0] != '=';
                }

 				return true;
 			}

 			return false;
	 	}

	 	public static string SetBoolParam(string OriginalParams, string BoolParam, bool bTrue)
	 	{
	 		string NewParams = OriginalParams;

 			if(NewParams.Contains("--" + BoolParam))
 			{
 				if(!bTrue)
 				{
 					NewParams = NewParams.Replace(" --" + BoolParam, string.Empty);
 					NewParams = NewParams.Replace("--" + BoolParam, string.Empty);
 				}
 			}
 			else
 			{
 				if(bTrue)
 				{
 					NewParams += " --" + BoolParam;
 				}
 			}

 			return NewParams;
	 	}

	 	public static bool IsStringParamSet(string AllParams, string ParamKey)
	 	{
 			if(AllParams.Contains("--" + ParamKey + "="))
 			{
 				return true;
 			}

 			return false;
	 	}

	 	public static string GetStringParam(string AllParams, string ParamKey)
	 	{
            string Result = string.Empty;
 			
            if(AllParams.Contains("--" + ParamKey + "="))
 			{
 			    int StartingPos = 1, EndingPos = -1;

 				StartingPos = AllParams.IndexOf("--" + ParamKey + "=") + ("--" + ParamKey + "=").Length;

                string StartSubstring = AllParams.Substring(StartingPos);

 			    EndingPos = StartSubstring.IndexOf("--");
                if(EndingPos < 0)
                    EndingPos = StartSubstring.Length;
 				
                Result = StartSubstring.Substring(0, EndingPos);

                // Manually trim. We want to remove all space to the left/right of quotation marks, AND the quotation
                // marks themselves, but not the space within the quotation marks.

                while(Result.Length > 0 && (Result[Result.Length - 1] == ' ' || Result[Result.Length - 1] == '"'))
                {
                    bool bIsQuotation = Result[Result.Length - 1] == '"';
                    Result = Result.Remove(Result.Length - 1, 1);
                    if(bIsQuotation)
                        break;
                }

                while(Result.Length > 0 && (Result[0] == ' ' || Result[0] == '"'))
                {
                    bool bIsQuotation = Result[0] == '"';
                    Result = Result.Remove(0, 1);
                    if(bIsQuotation)
                        break;
                }
 			}

 			return Result;
	 	}

	 	public static string SetStringParam(string AllParams, string ParamKey, string ParamValue)
	 	{
	 		string NewParams = AllParams;

 			if(NewParams.Contains("--" + ParamKey + "="))
 			{
 				NewParams = ClearParam(AllParams, ParamKey);
 			}

 			if(ParamValue != string.Empty)
 			{
                string MungedParamValue = ParamValue;
                if(!MungedParamValue.StartsWith("\""))
                    MungedParamValue = "\"" + MungedParamValue;
                if(!MungedParamValue.EndsWith("\""))
                    MungedParamValue = MungedParamValue + "\"";
 				NewParams += " --" + ParamKey + "=" + MungedParamValue;
 			}

 			return NewParams;
	 	}
	}

	[XmlRoot("IgorModuleList")]
	public class IgorModuleList
	{
		public class ModuleItem
		{
			[XmlAttribute("ModuleName")]
			public string ModuleName;
			[XmlAttribute("ModuleDescriptorRelativePath")]
			public string ModuleDescriptorRelativePath;
		}

		[XmlArray("Modules")]
		[XmlArrayItem("Module")]
		public List<ModuleItem> Modules = new List<ModuleItem>();

	 	public void Save(string path)
	 	{
	 		XmlSerializer serializer = new XmlSerializer(typeof(IgorModuleList));

	 		IgorUtils.DeleteFile(path);

	 		using(FileStream stream = new FileStream(path, FileMode.Create))
	 		{
	 			serializer.Serialize(stream, this);
	 		}
	 	}
	 
	 	public static IgorModuleList Load(string path)
	 	{
	 		XmlSerializer serializer = new XmlSerializer(typeof(IgorModuleList));

	 		if(File.Exists(path))
			{
				File.SetAttributes(path, System.IO.FileAttributes.Normal);
			
	 		    using(var stream = new FileStream(path, FileMode.Open))
	 		    {
	 			    return serializer.Deserialize(stream) as IgorModuleList;
	 		    }
            }
            
            return null;
	 	}
	}

	[XmlRoot("IgorModuleDescriptor")]
	public class IgorModuleDescriptor
	{
		public string ModuleName;
		public int ModuleVersion;

		[XmlArray("ModuleFiles")]
		[XmlArrayItem("File")]
		public List<string> ModuleFiles = new List<string>();

		[XmlArray("ModuleDependencies")]
		[XmlArrayItem("Module")]
		public List<string> ModuleDependencies = new List<string>();

	 	public void Save(string path)
	 	{
	 		XmlSerializer serializer = new XmlSerializer(typeof(IgorModuleDescriptor));

	 		IgorUtils.DeleteFile(path);

	 		using(FileStream stream = new FileStream(path, FileMode.Create))
	 		{
	 			serializer.Serialize(stream, this);
	 		}
	 	}
	 
	 	public static IgorModuleDescriptor Load(string path)
	 	{
	 		XmlSerializer serializer = new XmlSerializer(typeof(IgorModuleDescriptor));

	 		if(File.Exists(path))
			{
				File.SetAttributes(path, System.IO.FileAttributes.Normal);
			}

	 		using(var stream = new FileStream(path, FileMode.Open))
	 		{
	 			return serializer.Deserialize(stream) as IgorModuleDescriptor;
	 		}
	 	}
	}

    [System.Serializable]
	public class IgorPersistentJobConfig
	{
		public string JobCommandLineParams = "";
		public string JobName = "";
	}

	[XmlRoot("IgorJobConfig")]
	public class IgorJobConfig
	{
		public static string IgorJobConfigPath = "IgorJob.xml";
		public static IgorJobConfig InternalOverride = null;

		public IgorPersistentJobConfig Persistent = new IgorPersistentJobConfig();

		public int LastPriority = -999999;
		public int LastIndexInPriority = -999999;

		public bool bIsRunning = false;
		public bool bMenuTriggered = false;

	 	public void Save(string path)
	 	{
	 		XmlSerializer serializer = new XmlSerializer(typeof(IgorJobConfig));
	 		using(FileStream stream = new FileStream(path, FileMode.Create))
	 		{
	 			serializer.Serialize(stream, this);
	 		}
	 	}
	 
	 	public static IgorJobConfig Load(string path)
	 	{
	 		XmlSerializer serializer = new XmlSerializer(typeof(IgorJobConfig));
	 		using(var stream = new FileStream(path, FileMode.Open))
	 		{
	 			return serializer.Deserialize(stream) as IgorJobConfig;
	 		}
	 	}

	 	public static IgorJobConfig GetConfig()
	 	{
	 		if(InternalOverride != null)
	 		{
	 			return InternalOverride;
	 		}

	 		if(File.Exists(IgorJobConfigPath))
	 		{
				return IgorJobConfig.Load(IgorJobConfigPath);
			}
			else
			{
				IgorJobConfig NewConfig = new IgorJobConfig();

				NewConfig.Save(IgorJobConfigPath);

				return NewConfig;
			}
	 	}

	 	public static void Cleanup()
	 	{
	 		if(File.Exists(IgorJobConfigPath))
	 		{
				IgorUtils.DeleteFile(IgorJobConfigPath);
	 		}
	 	}

	 	public static bool IsBoolParamSet(string BoolParam)
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			return IgorUtils.IsBoolParamSet(Inst.Persistent.JobCommandLineParams, BoolParam);
	 		}

	 		return false;
	 	}

	 	public static void SetBoolParam(string BoolParam, bool bTrue)
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			Inst.Persistent.JobCommandLineParams = IgorUtils.SetBoolParam(Inst.Persistent.JobCommandLineParams, BoolParam, bTrue);

	 			Inst.Save(IgorJobConfigPath);
	 		}
	 	}

		public static bool IsStringParamSet(string StringParam)
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			return IgorUtils.IsStringParamSet(Inst.Persistent.JobCommandLineParams, StringParam);
	 		}

	 		return false;
	 	}

	 	public static string GetStringParam(string ParamKey)
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			return IgorUtils.GetStringParam(Inst.Persistent.JobCommandLineParams, ParamKey);
	 		}

	 		return "";
	 	}

	 	public static void SetStringParam(string ParamKey, string ParamValue)
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			Inst.Persistent.JobCommandLineParams = IgorUtils.SetStringParam(Inst.Persistent.JobCommandLineParams, ParamKey, ParamValue);

	 			Inst.Save(IgorJobConfigPath);
	 		}
	 	}

	 	public static int GetLastPriority()
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			return Inst.LastPriority;
	 		}

	 		return 0;
	 	}

	 	public static void SetLastPriority(int NewLastPriority)
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			Inst.LastPriority = NewLastPriority;

	 			Inst.Save(IgorJobConfigPath);
	 		}
	 	}

	 	public static int GetLastIndexInPriority()
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			return Inst.LastIndexInPriority;
	 		}

	 		return 0;
	 	}

	 	public static void SetLastIndexInPriority(int NewLastIndex)
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			Inst.LastIndexInPriority = NewLastIndex;

	 			Inst.Save(IgorJobConfigPath);
	 		}
	 	}

	 	public static bool GetIsRunning()
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			return Inst.bIsRunning;
	 		}

	 		return false;
	 	}

	 	public static void SetIsRunning(bool bRunning)
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			Inst.bIsRunning = bRunning;

	 			Inst.Save(IgorJobConfigPath);
	 		}
	 	}

	 	public static bool GetWasMenuTriggered()
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			return Inst.bMenuTriggered;
	 		}

	 		return false;
	 	}

	 	public static void SetWasMenuTriggered(bool bMenu)
	 	{
	 		IgorJobConfig Inst = GetConfig();

	 		if(Inst != null)
	 		{
	 			Inst.bMenuTriggered = bMenu;

	 			Inst.Save(IgorJobConfigPath);
	 		}
	 	}
	}

	[InitializeOnLoad]
	public class IgorUpdater
	{
		static string kPrefix = "Igor_";

		[PreferenceItem("Igor")]
		static void PreferencesGUI()
		{
			bDontUpdate = EditorGUILayout.Toggle(new GUIContent("Don't auto-update", "No updating Igor files from local or remote sources"), bDontUpdate);
			bAlwaysUpdate = EditorGUILayout.Toggle(new GUIContent("Always update", "Update even if the versions match"), bAlwaysUpdate);
			bLocalDownload = EditorGUILayout.Toggle(new GUIContent("Local update", "Update from local directory instead of remotely from GitHub"), bLocalDownload);
			LocalPrefix = EditorGUILayout.TextField(new GUIContent("Local Update Directory", "This is the local directory (relative to the project root) to pull from when Local Update is enabled."), LocalPrefix);
		}

		public static bool bDontUpdate
		{
			get { return EditorPrefs.GetBool(kPrefix + "bDontUpdate", false); }
			set { EditorPrefs.SetBool(kPrefix + "bDontUpdate", value); }
		}

		public static bool bAlwaysUpdate
		{
			get { return EditorPrefs.GetBool(kPrefix + "bAlwaysUpdate", false); }
			set { EditorPrefs.SetBool(kPrefix + "bAlwaysUpdate", value); }
		}

		public static bool bLocalDownload
		{
			get { return EditorPrefs.GetBool(kPrefix + "bLocalDownload", false); }
			set { EditorPrefs.SetBool(kPrefix + "bLocalDownload", value); }
		}

		public static string LocalPrefix
		{
			get { return EditorPrefs.GetString(kPrefix + "LocalPrefix", ""); }
			set { EditorPrefs.SetString(kPrefix + "LocalPrefix", value); }
		}

		static IgorUpdater()
		{
			ServicePointManager.ServerCertificateValidationCallback +=
			delegate(object sender, X509Certificate certificate,
			                        X509Chain chain,
			                        System.Net.Security.SslPolicyErrors sslPolicyErrors)
			    {
			        return true;
			    };

			EditorApplication.update += CheckIfResuming;
		}

		private const int Version = 18;

		public static string BaseIgorDirectory = Path.Combine("Assets", Path.Combine("Editor", "Igor"));
		public static string RemotePrefix = "https://raw.githubusercontent.com/mikamikem/Igor/master/";
		public static string TempLocalDirectory = "IgorTemp/";

		public static string IgorUpdaterFilename = "IgorUpdater.cs";
		public static string IgorModulesListFilename = "IgorModulesList.xml";
		public static string InstalledModulesListPath = Path.Combine(BaseIgorDirectory, IgorModulesListFilename);
		public static string IgorLocalModulesListFilename = "IgorLocalModulesList.xml";
		public static string InstalledLocalModulesListPath = Path.Combine(BaseIgorDirectory, IgorLocalModulesListFilename);
		private static IgorModuleList SharedModuleListInst = null;

		public static string LocalModuleRoot = Path.Combine(BaseIgorDirectory, "Modules");
		public static string RemoteRelativeModuleRoot = "Modules/";
		public static string CoreModuleName = "Core.Core";

		public static IIgorCore Core = null;

        private static List<string> UpdatedContent = new List<string>(); 
		private static List<string> UpdatedModules = new List<string>();

		public static bool bTriggerConfigWindowRefresh = false;

		[MenuItem("Window/Igor/Check For Updates %i", false, 2)]
		public static void MenuCheckForUpdates()
		{
			bTriggerConfigWindowRefresh = true;
			
			CheckForUpdates(true, true);
		}

		// Returns true if files were updated
		public static bool CheckForUpdates(bool bForce = false, bool bFromMenu = false, bool bSynchronous = false)
		{
			IgorJobConfig.SetWasMenuTriggered(bFromMenu);

			if(!bDontUpdate || bForce)
			{
                UpdatedContent.Clear();

				bool bNeedsRebuild = SelfUpdate();

				bNeedsRebuild = UpdateCore() || bNeedsRebuild;
				bNeedsRebuild = UpdateModules() || bNeedsRebuild;

				if(bNeedsRebuild)
				{
					IgorJobConfig.SetBoolParam("restartingfromupdate", true);

                    string UpdatedContentString = "The following Igor content was ";

                    if(bAlwaysUpdate)
                    {
                    	UpdatedContentString += "forcibly ";
                    }

                    UpdatedContentString += "updated via ";

                    if(bLocalDownload)
                    {
                    	UpdatedContentString += "local copy from " + LocalPrefix;
                    }
                    else
                    {
                    	UpdatedContentString += "remote copy from GitHub";
                    }

                    UpdatedContentString += ", refreshing AssetDatabase:\n";

                    foreach(var content in UpdatedContent)
                    {
                        UpdatedContentString += content + "\n";
                    }

                    Debug.Log(UpdatedContentString);

                    ImportAssetOptions options = bSynchronous ? ImportAssetOptions.ForceSynchronousImport : ImportAssetOptions.Default;
					AssetDatabase.Refresh(options);
				}

				return bNeedsRebuild;
			}

			return false;
		}

		public static void FindCore()
		{
			if(Core == null)
			{
				List<Type> ActiveCores = IgorUtils.GetTypesInheritFrom<IIgorCore>();

				if(ActiveCores.Count > 0)
				{
					Core = (IIgorCore)Activator.CreateInstance(ActiveCores[0]);
				}
			}
		}

		public static void CleanupTemp()
		{
			IgorUtils.DeleteDirectory(TempLocalDirectory);
		}

		public static int GetCurrentVersion()
		{
			return Version;
		}

		public static int GetVersionFromUpdaterFile(string Filename)
		{
			if(File.Exists(Filename))
			{
				string FileContents = File.ReadAllText(Filename);

			    if(!string.IsNullOrEmpty(FileContents))
			    {
			        int StartOfVersionNumber = FileContents.IndexOf("private const int Version = ") + "private const int Version = ".Length;

			        string VersionNumberString = FileContents.Substring(StartOfVersionNumber, FileContents.IndexOf(";", StartOfVersionNumber) - StartOfVersionNumber);

			        int VersionNumber = -1;

			        int.TryParse(VersionNumberString, out VersionNumber);

			        return VersionNumber;
			    }
			}

			return -1;
		}

		public static bool SelfUpdate()
		{
			bool bThrewException = false;

			string InstalledFilePath = Path.Combine(BaseIgorDirectory, IgorUpdaterFilename);

			try
			{
				string LocalUpdater = IgorUtils.DownloadFileForUpdate(IgorUpdaterFilename);

				if(File.Exists(LocalUpdater))
				{
					int NewVersion = GetVersionFromUpdaterFile(LocalUpdater);

					if(NewVersion > Version || bAlwaysUpdate)
					{
						if(File.Exists(InstalledFilePath))
						{
							IgorUtils.DeleteFile(InstalledFilePath);
						}

						File.Copy(LocalUpdater, InstalledFilePath);

						return true;
					}
				}
			}
			catch(TimeoutException to)
			{
				if(!File.Exists(InstalledFilePath))
				{
					Debug.LogError("Caught exception while self-updating.  Exception is " + (to == null ? "NULL exception!" : to.ToString()));

					bThrewException = true;

					CleanupTemp();
				}
			}
			catch(Exception e)
			{
				Debug.LogError("Caught exception while self-updating.  Exception is " + (e == null ? "NULL exception!" : e.ToString()));

				bThrewException = true;

				CleanupTemp();
			}

			if(!IgorJobConfig.GetWasMenuTriggered())
			{
				if(bThrewException)
				{
                    Debug.LogError("Exiting EditorApplication because an exception was thrown.");
					EditorApplication.Exit(-1);
				}
			}

			return false;
		}

		public static bool UpdateCore()
		{
			bool bThrewException = false;

			string LocalModulesList = IgorUtils.DownloadFileForUpdate(IgorModulesListFilename);

			try
			{
				if(File.Exists(LocalModulesList))
				{
					if(File.Exists(InstalledModulesListPath))
					{
						IgorUtils.DeleteFile(InstalledModulesListPath);
					}

					File.Copy(LocalModulesList, InstalledModulesListPath);
				}

				UpdatedModules.Clear();

				if(UpdateModule(CoreModuleName))
				{
					return true;
				}
			}
			catch(TimeoutException to)
			{
				if(!File.Exists(LocalModulesList))
				{
					Debug.LogError("Caught exception while self-updating.  Exception is " + (to == null ? "NULL exception!" : to.ToString()));

					bThrewException = true;

					CleanupTemp();
				}
			}
			catch(Exception e)
			{
				Debug.LogError("Caught exception while updating core.  Exception is " + (e == null ? "NULL exception!" : e.ToString()));

				bThrewException = true;
				
				CleanupTemp();
			}

			if(!IgorJobConfig.GetWasMenuTriggered())
			{
				if(bThrewException)
				{
                    Debug.LogError("Exiting EditorApplication because an exception was thrown.");
					EditorApplication.Exit(-1);
				}
			}

			return false;
		}

		public static bool UpdateModule(string ModuleName)
		{
			bool bUpdated = false;

			if(File.Exists(InstalledModulesListPath))
			{
				SharedModuleListInst = IgorModuleList.Load(InstalledModulesListPath);
			}

			if(SharedModuleListInst != null)
			{
				foreach(IgorModuleList.ModuleItem CurrentModule in SharedModuleListInst.Modules)
				{
					if(CurrentModule.ModuleName == ModuleName)
					{
						string ModuleDescriptor = IgorUtils.DownloadFileForUpdate(RemoteRelativeModuleRoot + CurrentModule.ModuleDescriptorRelativePath);
						string CurrentModuleDescriptor = Path.Combine(LocalModuleRoot, CurrentModule.ModuleDescriptorRelativePath);

						if(File.Exists(ModuleDescriptor))
						{
							IgorModuleDescriptor CurrentModuleDescriptorInst = null;
							IgorModuleDescriptor NewModuleDescriptorInst = IgorModuleDescriptor.Load(ModuleDescriptor);

							if(File.Exists(CurrentModuleDescriptor))
							{
								CurrentModuleDescriptorInst = IgorModuleDescriptor.Load(CurrentModuleDescriptor);
							}

							if(NewModuleDescriptorInst != null)
							{
								if(UpdatedModules.Contains(NewModuleDescriptorInst.ModuleName))
								{
									return false;
								}

								UpdatedModules.Add(NewModuleDescriptorInst.ModuleName);

								if(NewModuleDescriptorInst.ModuleDependencies.Count > 0)
								{
									foreach(string CurrentDependency in NewModuleDescriptorInst.ModuleDependencies)
									{
										bUpdated = UpdateModule(CurrentDependency) || bUpdated;
									}
								}

								int NewVersion = NewModuleDescriptorInst.ModuleVersion;

								if(CurrentModuleDescriptorInst == null || NewVersion > CurrentModuleDescriptorInst.ModuleVersion || bAlwaysUpdate)
								{
									bUpdated = true;
                                    UpdatedContent.Add(ModuleName);

									if(CurrentModuleDescriptorInst != null)
									{
										foreach(string ModuleFile in CurrentModuleDescriptorInst.ModuleFiles)
										{
                                            string LocalFile = IgorUtils.GetLocalFileFromModuleFilename(ModuleFile);

											string FullLocalPath = Path.Combine(LocalModuleRoot, Path.Combine(Path.GetDirectoryName(CurrentModule.ModuleDescriptorRelativePath), LocalFile));

											if(File.Exists(FullLocalPath))
											{
												IgorUtils.DeleteFile(FullLocalPath);
											}
										}

										IgorUtils.DeleteFile(CurrentModuleDescriptor);
									}

									if(!Directory.Exists(Path.GetDirectoryName(CurrentModuleDescriptor)))
									{
										Directory.CreateDirectory(Path.GetDirectoryName(CurrentModuleDescriptor));
									}

									File.Copy(ModuleDescriptor, CurrentModuleDescriptor);

									foreach(string ModuleFile in NewModuleDescriptorInst.ModuleFiles)
									{
                                        bool bIsExternal = false;

                                        string LocalFile = IgorUtils.GetLocalFileFromModuleFilename(ModuleFile);

										string FullLocalPath = Path.Combine(LocalModuleRoot, Path.Combine(Path.GetDirectoryName(CurrentModule.ModuleDescriptorRelativePath), LocalFile));
										
                                        string RemotePath = IgorUtils.GetRemoteFileFromModuleFilename(RemoteRelativeModuleRoot + Path.GetDirectoryName(CurrentModule.ModuleDescriptorRelativePath) + "/", ModuleFile, ref bIsExternal);

										if(LocalFile.StartsWith("."))
										{
											string Base = Path.Combine(LocalModuleRoot, Path.GetDirectoryName(CurrentModule.ModuleDescriptorRelativePath));
											string NewLocalFile = LocalFile;
											int FirstIndex = NewLocalFile.IndexOf("../");

											while(FirstIndex != -1)
											{
												int LastIndex = Base.LastIndexOf(Path.DirectorySeparatorChar);

												if(LastIndex != -1)
												{
													Base = Base.Substring(0, LastIndex);
												}

												NewLocalFile = NewLocalFile.Substring(3);

												FirstIndex = NewLocalFile.IndexOf("../");
											}

											FullLocalPath = Path.Combine(Base, NewLocalFile);
											RemotePath = IgorUtils.GetRemoteFileFromModuleFilename(RemoteRelativeModuleRoot + Path.GetDirectoryName(CurrentModule.ModuleDescriptorRelativePath) + "/", ModuleFile, ref bIsExternal);
										}

                                        string TempDownloadPath = "";
                                        if(bIsExternal)
										{
											TempDownloadPath = IgorUtils.DownloadFileForUpdate(Path.Combine(Path.GetDirectoryName(CurrentModule.ModuleDescriptorRelativePath), LocalFile), RemotePath);
										}
										else
										{
											TempDownloadPath = IgorUtils.DownloadFileForUpdate(RemotePath);
										}

										if(File.Exists(FullLocalPath))
										{
											IgorUtils.DeleteFile(FullLocalPath);
										}

										if(!Directory.Exists(Path.GetDirectoryName(FullLocalPath)))
										{
											Directory.CreateDirectory(Path.GetDirectoryName(FullLocalPath));
										}

										if(File.Exists(TempDownloadPath))
										{
											File.Copy(TempDownloadPath, FullLocalPath);
										}
									}
								}
							}
						}

						return bUpdated;
					}
				}
			}

			return false;
		}

		public static bool UpdateModules()
		{
			bool bThrewException = false;

			try
			{
				FindCore();

				if(Core != null)
				{
					bool bUpdated = false;

					UpdatedModules.Clear();

					foreach(string CurrentModule in Core.GetEnabledModuleNames())
					{
						bUpdated = UpdateModule(CurrentModule) || bUpdated;
					}

					if(bUpdated)
					{
						return true;
					}
				}
			}
			catch(TimeoutException)
			{
				// We should eventually handle this case by triggering a re-attempt
			}
			catch(Exception e)
			{
				Debug.LogError("Caught exception while updating modules.  Exception is " + (e == null ? "NULL exception!" : e.ToString()));

				bThrewException = true;

				CleanupTemp();
			}

			if(!IgorJobConfig.GetWasMenuTriggered())
			{
				if(bThrewException)
				{
                    Debug.LogError("Exiting EditorApplication because an exception was thrown.");
					EditorApplication.Exit(-1);
				}
			}

			return false;
		}

		public static void CheckIfResuming()
		{
		    if(!EditorApplication.isCompiling)
		    {
		        bool bThrewException = false;

		        EditorApplication.update -= CheckIfResuming;

		        try
		        {
		            FindCore();

		            if(IgorJobConfig.IsBoolParamSet("restartingfromupdate") || IgorJobConfig.IsBoolParamSet("updatebeforebuild") || Core == null)
		            {
		                IgorJobConfig.SetBoolParam("restartingfromupdate", false);

		                if(!CheckForUpdates())
		                {
		                    if(IgorJobConfig.IsBoolParamSet("updatebeforebuild"))
		                    {
		                        IgorJobConfig.SetBoolParam("updatebeforebuild", false);

		                        if(Core != null)
		                        {
		                            Core.RunJobInst();
		                        }
		                        else
		                        {
		                            Debug.LogError("Something went really wrong.  We don't have Igor's core, but we've already finished updating everything.  Report this with your logs please!");
		                        }
		                    }
		                }
		            }
		        }
		        catch(Exception e)
		        {
		            Debug.LogError("Caught exception while resuming from updates.  Exception is " + (e == null ? "NULL exception!" : e.ToString()));

		            bThrewException = true;
		        }

		        if(!IgorJobConfig.GetWasMenuTriggered())
		        {
		            if(bThrewException)
		            {
                        Debug.LogError("Exiting EditorApplication because an exception was thrown.");
		                EditorApplication.Exit(-1);
		            }
		        }
		    }
		}

		public static void GenerateModuleList()
		{
			IgorModuleList NewInst = new IgorModuleList();

			IgorModuleList.ModuleItem NewItem = new IgorModuleList.ModuleItem();

			NewItem.ModuleName = "Core.Core";
			NewItem.ModuleDescriptorRelativePath = "Core/Core/Core.mod";

			NewInst.Modules.Add(NewItem);

			NewInst.Save(IgorModulesListFilename);
		}
	}
}