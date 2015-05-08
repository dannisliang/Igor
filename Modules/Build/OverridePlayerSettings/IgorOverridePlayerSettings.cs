using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System;
using System.Reflection;
using System.Xml.Serialization;

namespace Igor
{
	public class IgorOverridePlayerSettings : IgorModuleBase
	{
        static string[] kProjectSettingFiles =
        {
            "TimeManager.asset",
            "TagManager.asset",
            "QualitySettings.asset",
            "ProjectSettings.asset",
            "Physics2DSettings.asset",
            "NetworkManager.asset",
            "NavMeshLayers.asset",
            "InputManager.asset",
            "GraphicsSettings.asset",
            "EditorSettings.asset",
            "EditorBuildSettings.asset",
            "DynamicsManager.asset",
            "AudioManager.asset",
        };

	    static string kPlayerSettingsFolder = "Igor Override Player Settings";

        static string kIgorProjectSettingExtension = ".igorplayersettings";

        public static string PlayerSettingFilesToOverrideFlag = "player_settings_to_override";
		public static string PlayerSettingsPathFlag = "player_settings_file";
        private int SelectedProjectSettingsAsInt;

		public override string GetModuleName()
		{
			return "Build.OverridePlayerSettings";
		}

		public override void RegisterModule()
		{
			IgorCore.RegisterNewModule(this);
		}

		public override void ProcessArgs(IIgorStepHandler StepHandler)
		{
			if(IgorJobConfig.IsStringParamSet(PlayerSettingFilesToOverrideFlag) && IgorJobConfig.IsStringParamSet(PlayerSettingsPathFlag))
			{
				IgorCore.SetModuleActiveForJob(this);
				StepHandler.RegisterJobStep(IgorBuildCommon.OverridePlayerSettings, this, OverridePlayerSettings);
			}
		}

		public override string DrawJobInspectorAndGetEnabledParams(string CurrentParams)
		{
			string EnabledParams = CurrentParams;

            IgorConfigWindow ConfigurationWindow = IgorConfigWindow.OpenOrGetConfigWindow();
            IgorPersistentJobConfig CurrentJob = ConfigurationWindow.CurrentJobInst;
		    string CurrentJobAsString = CurrentJob != null ? CurrentJob.JobName : string.Empty;
            string TargetDirectory = kPlayerSettingsFolder + "/" + CurrentJobAsString;
		    
            GUILayout.BeginHorizontal();
		    {
                string SelectedProjectSettingsAsString = IgorUtils.GetStringParam(EnabledParams, PlayerSettingFilesToOverrideFlag).Trim('"');

                if(!string.IsNullOrEmpty(SelectedProjectSettingsAsString))
                {
                    int OutResult = 0;
                    if(Int32.TryParse(SelectedProjectSettingsAsString, out OutResult))
                    {
                        SelectedProjectSettingsAsInt = OutResult;
                    }
                }

                int newValue = EditorGUILayout.MaskField(SelectedProjectSettingsAsInt, kProjectSettingFiles);

                if(newValue != SelectedProjectSettingsAsInt)
                {
                    SelectedProjectSettingsAsInt = newValue;
                    if(newValue != 0)
                    {
                        EnabledParams = IgorUtils.SetStringParam(EnabledParams, PlayerSettingFilesToOverrideFlag, SelectedProjectSettingsAsInt.ToString());
                        EnabledParams = IgorUtils.SetStringParam(EnabledParams, PlayerSettingsPathFlag, '"' + TargetDirectory + '"');
                    }
                    else
                    {
                        EnabledParams = IgorUtils.ClearParam(EnabledParams, PlayerSettingFilesToOverrideFlag);
                        EnabledParams = IgorUtils.ClearParam(EnabledParams, PlayerSettingsPathFlag);
                    }
                }
		    }
            GUILayout.EndHorizontal();
            
            string FilesToSave = string.Empty;
            for(int i = 0; i < kProjectSettingFiles.Length; ++i)
            {
                if(((1 << i) & SelectedProjectSettingsAsInt) != 0)
                {
                    FilesToSave += ((string.IsNullOrEmpty(FilesToSave) ? string.Empty : ", ") + kProjectSettingFiles[i].Replace(".asset", string.Empty));
                }
            }

            GUILayout.Space(5f);
            GUILayout.Label("Files to save: " + FilesToSave);

            if(Directory.Exists(TargetDirectory))
            {
                GUILayout.Space(5f);
                string[] SourceFilesPaths = Directory.GetFiles(TargetDirectory);

                string ExistingOverrides = string.Empty;
		        foreach(string SourceFilePath in SourceFilesPaths)
                {
                    ExistingOverrides += ((string.IsNullOrEmpty(ExistingOverrides) ? string.Empty : ", ") + Path.GetFileName(SourceFilePath).Replace(kIgorProjectSettingExtension, string.Empty));
                }

                GUILayout.Label("Existing overrides on disk: " + ExistingOverrides);    
                GUILayout.Space(5f);
            }

		    GUILayout.BeginHorizontal();
		    {
		        GUI.enabled = CurrentJob != null && SelectedProjectSettingsAsInt != 0;
		        if(GUILayout.Button("Save", GUILayout.ExpandWidth(false)))
		        {
		            if(!Directory.Exists(kPlayerSettingsFolder))
		            {
		                Directory.CreateDirectory(kPlayerSettingsFolder);
		            }

		            IgorUtils.DeleteDirectory(TargetDirectory);
		            Directory.CreateDirectory(TargetDirectory);

		            string[] SourceFilesPaths = Directory.GetFiles("ProjectSettings");
		            foreach(string SourceFilePath in SourceFilesPaths)
		            {
		                if(!SourceFilePath.EndsWith(".meta"))
		                {
                            string FileName = Path.GetFileName(SourceFilePath);
                            
                            int IndexInKnownAssetList = Array.IndexOf(kProjectSettingFiles, FileName, 0, kProjectSettingFiles.Length);
                            if(IndexInKnownAssetList != -1)
                            {
                                if((((1 << IndexInKnownAssetList) & SelectedProjectSettingsAsInt) != 0) || SelectedProjectSettingsAsInt == -1) 
                                {
		                            string DestFilePath = SourceFilePath.Replace("ProjectSettings\\", string.Empty);
		                            DestFilePath = TargetDirectory + "/" + Path.ChangeExtension(DestFilePath, kIgorProjectSettingExtension);
		                            File.Copy(SourceFilePath, DestFilePath);
                                }
                            }
		                }
		            }

		            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
		        }

		        string Tooltip = Directory.Exists(TargetDirectory) ? string.Empty : "Expected PlayerSettings directory " + " doesn't exist.";

		        GUI.enabled &= Directory.Exists(TargetDirectory);
		        if(GUILayout.Button(new GUIContent("Load saved settings file", Tooltip), GUILayout.ExpandWidth(false)))
		        {
		            CopyStoredPlayerSettingsOverCurrent(TargetDirectory);
		        }

		        GUI.enabled = true;
		    }
            GUILayout.EndHorizontal();

			return EnabledParams;
		}

	    private static void CopyStoredPlayerSettingsOverCurrent(string TargetDirectory)
	    {
	        string[] SourceFilesPaths = Directory.GetFiles(TargetDirectory);
		    foreach(string SourceFilePath in SourceFilesPaths)
		    {
		        string DestFilePath = SourceFilePath.Replace(TargetDirectory, "ProjectSettings");
		        DestFilePath = Path.ChangeExtension(DestFilePath, ".asset");
		        File.Copy(SourceFilePath, DestFilePath, true);

                // We need to find the ProjectSettings file and locate the defines text manually because otherwise
                // the recompile (if it even triggers; it's inconsistent) won't use the new defines.
		        const string ScriptingDefineSymbolsTag = "scriptingDefineSymbols:\n";
		        if(DestFilePath.Contains("ProjectSettings.asset"))
		        {
		            string ProjectSettingsText = File.ReadAllText(SourceFilePath);
		            int StartIndex = ProjectSettingsText.IndexOf(ScriptingDefineSymbolsTag) + ScriptingDefineSymbolsTag.Length;
                    string StartOfDefinesBlock = ProjectSettingsText.Substring(StartIndex);

		            HashSet<BuildTargetGroup> MatchedBuildTargetGroups = new HashSet<BuildTargetGroup>();

		            string NextLine;
                    StringReader StringReader = new StringReader(StartOfDefinesBlock);
		            bool bContinue = true;
		            do
		            {
		                NextLine = StringReader.ReadLine();
		                if(NextLine != null)
		                {
		                    NextLine = NextLine.Trim();
		                    if(NextLine.Length > 0 && char.IsNumber(NextLine[0]))
		                    {
		                        int IndexOfColon = NextLine.IndexOf(':');
		                        string BuildGroupText = NextLine.Substring(0, IndexOfColon);
		                        string Define = NextLine.Substring(IndexOfColon + 1);

		                        int BuildGroupAsInt = 0;
                                Int32.TryParse(BuildGroupText, out BuildGroupAsInt);
		                        BuildTargetGroup TargetGroup = (BuildTargetGroup)BuildGroupAsInt;

		                        if(TargetGroup != BuildTargetGroup.Unknown)
		                        {
		                            PlayerSettings.SetScriptingDefineSymbolsForGroup(TargetGroup, Define);
		                            MatchedBuildTargetGroups.Add(TargetGroup);
		                        }
		                    }
                            else
		                    {
		                        bContinue = false;
		                    }
		                }
		            }
		            while(bContinue);

                    // Make sure we wipe out defines on any other build targets.
		            BuildTargetGroup[] AllTargetGroups = System.Enum.GetValues(typeof(BuildTargetGroup)) as BuildTargetGroup[];
		            foreach(BuildTargetGroup Group in AllTargetGroups)
		            {
		                if(!MatchedBuildTargetGroups.Contains(Group))
		                {
		                    PlayerSettings.SetScriptingDefineSymbolsForGroup(Group, string.Empty);
		                }
		            }
		        }
		    }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
	    }

	    public virtual bool OverridePlayerSettings()
	    {
	        string TargetDirectory = IgorJobConfig.GetStringParam(PlayerSettingsPathFlag);
	        TargetDirectory = TargetDirectory.Replace("\"", string.Empty);
	        string LogDetails = "Overriding default player settings with settings from directory " + TargetDirectory;
	        Log(LogDetails);

	        CopyStoredPlayerSettingsOverCurrent(TargetDirectory);
	        return true;
	    }
	}
}