﻿using UnityEditor;
using UnityEngine;

namespace ProBuilder.AssetUtility
{
	class PackageImporter : AssetPostprocessor
	{
		static readonly string[] k_AssetStoreInstallGuids = new string[]
		{
			"0472bdc8d6d15384d98f22ee34302f9c", // ProBuilderCore
			"b617d7797480df7499f141d87e13ebc5", // ProBuilderMeshOps
			"4df21bd079886d84699ca7be1316c7a7"  // ProBuilderEditor
		};

#pragma warning disable 414
		static readonly string[] k_PackageManagerInstallGuids = new string[]
		{
			"4f0627da958b4bb78c260446066f065f", // Core
			"9b27d8419276465b80eb88c8799432a1", // Mesh Ops
			"e98d45d69e2c4936a7382af00fd45e58", // Editor
		};
#pragma warning restore 414

		const string k_PackageManagerEditorCore = "e98d45d69e2c4936a7382af00fd45e58";
		const string k_AssetStoreEditorCore = "4df21bd079886d84699ca7be1316c7a7";

		internal static string EditorCorePackageManager { get { return k_PackageManagerEditorCore; } }
		internal static string EditorCoreAssetStore { get { return k_AssetStoreEditorCore; } }

		static PackageImporter()
		{
		}

		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			CheckEditorCoreEnabled();
		}

		static void CheckEditorCoreEnabled()
		{
			string editorCoreDllPath = AssetDatabase.GUIDToAssetPath(k_PackageManagerEditorCore);
			var importer = AssetImporter.GetAtPath(editorCoreDllPath) as PluginImporter;

			if (importer != null)
			{
				bool assetStoreInstall = AreAnyAssetsAreLoaded(k_AssetStoreInstallGuids);
				bool isEnabled = importer.GetCompatibleWithEditor();

				if (isEnabled == assetStoreInstall)
				{
					Debug.Log("AssetStore/UPM Change Detected: " + (isEnabled ? "  disabling UPM" : "  enabling UPM"));
					importer.SetCompatibleWithAnyPlatform(false);
					importer.SetCompatibleWithEditor(!assetStoreInstall);
					AssetDatabase.ImportAsset(editorCoreDllPath);


					if (isEnabled)
					{
						if (EditorUtility.DisplayDialog("Conflicting ProBuilder Install in Assets",
							"The Asset Store and Source versions of ProBuilder are incompatible with Package Manager. Would you like to convert your project to the Package Manager version of ProBuilder?",
							"Yes", "No"))
							AssetIdRemapUtility.OpenConversionEditor();
						else
							Debug.Log("ProBuilder Package Manager conversion process cancelled. You may initiate this conversion at a later time via Tools/ProBuilder/Repair/Convert to Package Manager.");
					}
				}
			}
		}

		internal static void SetEditorDllEnabled(string guid, bool isEnabled)
		{
			string dllPath = AssetDatabase.GUIDToAssetPath(guid);

			var importer = AssetImporter.GetAtPath(dllPath) as PluginImporter;

			if (importer != null)
			{
				importer.SetCompatibleWithAnyPlatform(false);
				importer.SetCompatibleWithEditor(isEnabled);
			}
		}

		internal static bool IsEditorPlugin(string guid)
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			var importer = AssetImporter.GetAtPath(path) as PluginImporter;
			if (!importer)
				return false;
			return importer.GetCompatibleWithEditor() && !importer.GetCompatibleWithAnyPlatform();
		}

		internal static void Reimport(string guid)
		{
			string dllPath = AssetDatabase.GUIDToAssetPath(guid);

			if(!string.IsNullOrEmpty(dllPath))
				AssetDatabase.ImportAsset(dllPath);
		}

		static bool AreAnyAssetsAreLoaded(string[] guids)
		{
			foreach (var id in guids)
			{
				if (AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(id)) != null)
					return true;
			}

			return false;
		}
	}
}