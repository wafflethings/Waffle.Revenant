using JetBrains.Annotations;
using Unity.Plastic.Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class StringBuilderExtensions
{
	public static int ExtIndexOf(this StringBuilder sb, string value, int startIndex, bool ignoreCase)
	{
		int index;
		int length = value.Length;
		int maxSearchLength = (sb.Length - length) + 1;

		if (ignoreCase)
		{
			for (int i = startIndex; i < maxSearchLength; ++i)
			{
				if (Char.ToLower(sb[i]) == Char.ToLower(value[0]))
				{
					index = 1;
					while ((index < length) && (Char.ToLower(sb[i + index]) == Char.ToLower(value[index])))
						++index;

					if (index == length)
						return i;
				}
			}

			return -1;
		}

		for (int i = startIndex; i < maxSearchLength; ++i)
		{
			if (sb[i] == value[0])
			{
				index = 1;
				while ((index < length) && (sb[i + index] == value[index]))
					++index;

				if (index == length)
					return i;
			}
		}

		return -1;
	}
}

public class UniversalPorter : MonoBehaviour
{
    static class IOUtils
    {
        private static IEnumerable<string> GetAllFilesRecursive(string path)
        {
            foreach (string file in Directory.GetFiles(path))
                yield return file;
            foreach (string folder in Directory.GetDirectories(path))
                foreach (string subFile in GetAllFilesRecursive(folder))
                    yield return subFile;
        }

        public static string[] GetAllFiles(string directory)
        {
			return Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
		}
    }

    static class FileUtils
    {
        public static string GetGuidFromMeta(string metaPath)
        {
            string allMeta = File.ReadAllText(metaPath);
            int guidIndex = allMeta.IndexOf("guid: ") + "guid: ".Length;
            return allMeta.Substring(guidIndex, 32);
        }

        public static string[] ValidGuidFileExtensions = new string[]
        {
			".meta",
		    ".mat",
		    ".anim",
		    ".prefab",
		    ".unity",
		    // ".asset",
		    ".guiskin",
		    ".fontsettings",
		    ".controller",
		};

        public static bool ApplyGuidChange(string filePath, Dictionary<string, string> guidMap)
		{
			StringBuilder text = new StringBuilder(File.ReadAllText(filePath));
			int currentIndex = text.ExtIndexOf("guid: ", 0, false);
			bool changed = false;

			while (currentIndex != -1)
			{
				string oldGuid = text.ToString(currentIndex + 6, 32);
				if (guidMap.TryGetValue(oldGuid, out string newGuid))
				{
					changed = true;
					int index = currentIndex + 6;
					for (int i = 0; i < 32; i++)
						text[i + index] = newGuid[i];
				}

				currentIndex = text.ExtIndexOf("guid: ", currentIndex + 1, false);
			}

			if (!changed)
				return false;

			File.WriteAllText(filePath, text.ToString());
			return true;
		}
	}

    public static string otherAssetsPath;
    public static string targetAssetsPath;

    public static string[] currentProjectAssets;
    public static string[] otherProjectAssets;

    static string ReadID(StringBuilder sb, int index)
    {
        StringBuilder num = new StringBuilder();
        while (char.IsDigit(sb[index]))
            num.Append(sb[index++]);
        return num.ToString();
    }

    // STEP 1

    static void ScriptPorting()
    {
        List<string> localScripts = currentProjectAssets.Where(path => path.EndsWith(".cs.meta")).ToList();
		List<string> otherScripts = otherProjectAssets.Where(path => path.EndsWith(".cs.meta")).ToList();
		Dictionary<string, string> guidChangeMap = new Dictionary<string, string>();

        try
        {
            bool notify = true;
            for (int i = 0; i < otherScripts.Count; i++)
            {
                string scriptName = Path.GetFileNameWithoutExtension(otherScripts[i]);
                if (EditorUtility.DisplayCancelableProgressBar("Mapping scripts", scriptName, (float)i / otherScripts.Count))
                    return;

                string localScriptPath = localScripts.Where(path => Path.GetFileNameWithoutExtension(path) == scriptName).FirstOrDefault();
                if (localScriptPath == null)
                {
                    if (notify)
                    {
                        if (!EditorUtility.DisplayDialog("Warning", $"Other project's {scriptName} script not found locally", "Ok", "Don't show again"))
                            notify = false;
                    }

                    continue;
                }

                string remoteGuid = FileUtils.GetGuidFromMeta(otherScripts[i]);
                string localGuid = FileUtils.GetGuidFromMeta(localScriptPath);
                guidChangeMap[remoteGuid] = localGuid;
            }
        }
        finally
        {
			EditorUtility.ClearProgressBar();
		}

        AssetDatabase.StartAssetEditing();
        try
        {
            List<string> filesToChange = IOUtils.GetAllFiles(targetAssetsPath).Where(path => FileUtils.ValidGuidFileExtensions.Contains(Path.GetExtension(path))).ToList();
            for (int i = 0; i <  filesToChange.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Porting", filesToChange[i], (float)i / filesToChange.Count);
                FileUtils.ApplyGuidChange(filesToChange[i], guidChangeMap);
			}
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
	}

	// STEP 2
	class ComponentInfo
	{
		public string id { get; set; }
		public string type { get; set; }
	}

	class PrefabNode
	{
		public string gameObjectName { get; set; }
		public string gameObjectId { get; set; }
		public string transformId { get; set; }
		public int rootOrder { get; set; }

		public List<ComponentInfo> components = new List<ComponentInfo>();
		public List<PrefabNode> children = new List<PrefabNode>();
	}

	static PrefabNode MakePrefabInfo(GameObject prefab, string realPath)
	{
		PrefabNode node = new PrefabNode();
		long id;

		if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out _, out id))
			node.gameObjectId = id.ToString();
		if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab.transform, out _, out id))
			node.transformId = id.ToString();
		node.gameObjectName = prefab.name;
		node.rootOrder = prefab.transform.GetSiblingIndex();

		foreach (var comp in prefab.GetComponents<Component>())
		{
			if (comp == null)
			{
				Debug.Log($"Invalid component in {realPath}, missing scripts?");
				node.components.Add(new ComponentInfo() { id = "", type = "" });
				continue;
			}

			if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(comp, out _, out id))
				node.components.Add(new ComponentInfo() { id = id.ToString(), type = comp.GetType().Name });
		}

		foreach (Transform child in prefab.transform)
		{
			node.children.Add(MakePrefabInfo(child.gameObject, realPath));
		}

		return node;
	}

	static void ProcessPath(string path, string exportPath)
	{
		if (!path.EndsWith(".prefab") && !path.ToLower().EndsWith(".fbx") && !path.ToLower().EndsWith(".obj"))
			return;

		string guid = AssetDatabase.AssetPathToGUID(path);
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
		PrefabNode node = MakePrefabInfo(prefab, path);
		File.WriteAllText(Path.Combine(exportPath, guid + ".json"), JsonConvert.SerializeObject(node));
	}

	[MenuItem("Tools/Export Prefab IDs of selected folders or files")]
	static void ExportIDs()
	{
		List<string> assetPaths = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets).Select(obj => AssetDatabase.GetAssetPath(obj)).ToList();
		if (assetPaths.Count == 0)
		{
			EditorUtility.DisplayDialog("Error", "No files/folders selected!", "Ok");
			return;
		}

		string exportPath = EditorUtility.OpenFolderPanel("Export path", Application.dataPath, "PrefabIDs");
		if (!Directory.Exists(exportPath))
		{
			EditorUtility.DisplayDialog("Error", "Export folder not found!", "Ok");
			return;
		}

		if (Directory.GetFiles(exportPath).Length != 0 || Directory.GetDirectories(exportPath).Length != 0)
		{
			if (!EditorUtility.DisplayDialog("Warning", "Export folder not empty, continue exporting?", "Ok", "Cancel"))
			{
				return;
			}
		}

		string[] paths = AssetDatabase.GetAllAssetPaths();

		try
		{
			foreach (string path in assetPaths)
			{
				if (AssetDatabase.IsValidFolder(path))
				{
					foreach (string asset in paths.Where(asset => asset.StartsWith(path) && !AssetDatabase.IsValidFolder(asset)))
					{
						EditorUtility.DisplayProgressBar("Exporting IDs", asset, 0);
						ProcessPath(asset, exportPath);
					}
				}
				else
				{
					EditorUtility.DisplayProgressBar("Exporting IDs", path, 0);
					ProcessPath(path, exportPath);
				}
			}
		}
		finally
		{
			EditorUtility.ClearProgressBar();
		}
	}

	string ReadId(StringBuilder file, int cursor)
	{
		StringBuilder id = new StringBuilder();
		while (char.IsDigit(file[cursor]) || file[cursor] == '-')
			id.Append(file[cursor++]);
		return id.ToString();
	}

	static void _CreateInfoMap(PrefabNode localNode, PrefabNode remoteNode, Dictionary<string, string> map)
	{
		if (!map.ContainsKey(remoteNode.transformId))
			map[remoteNode.transformId] = localNode.transformId;
		if (!map.ContainsKey(remoteNode.gameObjectId))
			map[remoteNode.gameObjectId] = localNode.gameObjectId;

		/*if (localNode.rootOrder != remoteNode.rootOrder)
		{
			Debug.LogError($"Incorrect root order between prefab nodes for {_createInfoMapPath}");
			return;
		}*/

		/*if (localNode.children.Count != remoteNode.children.Count)
		{
			Debug.LogError($"Incorrect child count between prefab nodes for {_createInfoMapPath}");
			return;
		}*/

		/*if (localNode.components.Count != remoteNode.components.Count)
		{
			int localRealCompCount = localNode.components.Where(comp => comp.id != "").Count();
			int remoteRealCompCount = remoteNode.components.Where(comp => comp.id != "").Count();
			if (localRealCompCount == remoteRealCompCount)
			{
				localNode.components = localNode.components.Where(comp => comp.id != "").ToList();
				remoteNode.components = remoteNode.components.Where(comp => comp.id != "").ToList();
			}
			else
			{
				Debug.LogError($"Incorrect component count between prefab nodes for {_createInfoMapPath}");
				return;
			}
		}*/

		// Component mapping
		Dictionary<string, List<string>> localComponentMap = new Dictionary<string, List<string>>();
		Dictionary<string, List<string>> remoteComponentMap = new Dictionary<string, List<string>>();
		for (int i = 0; i < localNode.components.Count; i++)
		{
			string compType = localNode.components[i].type;
			if (compType == "")
				continue;

			List<string> compMap = null;
			if (!localComponentMap.TryGetValue(compType, out compMap))
			{
				compMap = new List<string>();
				localComponentMap[compType] = compMap;
			}

			compMap.Add(localNode.components[i].id);
		}
		for (int i = 0; i < remoteNode.components.Count; i++)
		{
			string compType = remoteNode.components[i].type;
			if (compType == "")
				continue;

			List<string> compMap = null;
			if (!remoteComponentMap.TryGetValue(compType, out compMap))
			{
				compMap = new List<string>();
				remoteComponentMap[compType] = compMap;
			}

			compMap.Add(remoteNode.components[i].id);
		}

		foreach (var remoteComps in remoteComponentMap)
		{
			if (localComponentMap.TryGetValue(remoteComps.Key, out var localComps))
			{
				int limit = Math.Min(localComps.Count, remoteComps.Value.Count);
				for (int i = 0; i < limit; i++)
				{
					map[remoteComps.Value[i]] = localComps[i];
				}
			}
		}

		// Map objects based on name and order
		Dictionary<string, int> objectNameSkipCount = new Dictionary<string, int>();
		for (int i = 0; i < remoteNode.children.Count; i++)
		{
			PrefabNode remoteChild = remoteNode.children[i];
			int skipCount = 0;
			objectNameSkipCount.TryGetValue(remoteChild.gameObjectName, out skipCount);

			PrefabNode localChild = localNode.children.Where(o => o.gameObjectName == remoteChild.gameObjectName).Skip(skipCount).FirstOrDefault();
			if (localChild != null)
				_CreateInfoMap(localChild, remoteChild, map);

			objectNameSkipCount[remoteChild.gameObjectName] = skipCount + 1;
		}

		/*for (int i = 0; i < remoteNode.components.Count; i++)
		{
			if (remoteNode.components[i].id == "")
			{
				Debug.Log($"Missing component script for remote prefab node {_createInfoMapPath}");
				continue;
			}
			if (localNode.components[i].id == "")
			{
				Debug.Log($"Missing component script for local prefab node {_createInfoMapPath}");
				continue;
			}
			if (map.ContainsKey(remoteNode.components[i].id))
				continue;

			map[remoteNode.components[i].id] = localNode.components[i].id;
		}

		for (int i = 0; i < remoteNode.children.Count; i++)
		{
			_CreateInfoMap(localNode.children[i], remoteNode.children[i], map);
		}*/
	}

	static string _createInfoMapPath = "";
	static void CreateInfoMap(PrefabNode localNode, PrefabNode remoteNode, Dictionary<string, string> map, string assetPath)
	{
		_createInfoMapPath = assetPath;
		_CreateInfoMap(localNode, remoteNode, map);
	}

	static void PortIDs(string path, string otherIdPath)
    {
        StringBuilder sb = new StringBuilder(File.ReadAllText(path));
		Dictionary<string, Dictionary<string, string>> map = new Dictionary<string, Dictionary<string, string>>();
		List<string> guidIgnore = new List<string>();

		int chunkCursor = 0;
		List<string> chunks = new List<string>();

		string fileIdText = "{fileID: ";
		int nextIdIndex = sb.ExtIndexOf(fileIdText, 0, false);
		while (nextIdIndex != -1)
		{
			nextIdIndex += fileIdText.Length;
			int idChunkIndex = nextIdIndex;
			string id = ReadID(sb, nextIdIndex);
			nextIdIndex += id.Length;
			if (sb.Length > (nextIdIndex + ", guid: ".Length) && sb.ToString(nextIdIndex, ", guid: ".Length) == ", guid: ")
			{
				int rightChunkIndex = nextIdIndex;
				nextIdIndex += ", guid: ".Length;
				string guid = sb.ToString(nextIdIndex, 32);

				if (!guidIgnore.Contains(guid))
				{
					Dictionary<string, string> conversionMap = null;
					if (!map.TryGetValue(guid, out conversionMap))
					{
						string fullInfoPath = Path.Combine(otherIdPath, guid + ".json");
						if (!File.Exists(fullInfoPath))
						{
							Debug.Log($"Prefab info with GUID {guid} not found");
							guidIgnore.Add(guid);
						}
						else
						{
							string assetPath = AssetDatabase.GUIDToAssetPath(guid);
							if (string.IsNullOrEmpty(assetPath))
							{
								Debug.Log($"Local prefab with GUID {guid} not found");
								guidIgnore.Add(guid);
							}
							else
							{
								PrefabNode localNode = MakePrefabInfo(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath), assetPath);
								PrefabNode remoteNode = JsonConvert.DeserializeObject<PrefabNode>(File.ReadAllText(fullInfoPath));

								conversionMap = new Dictionary<string, string>();
								CreateInfoMap(localNode, remoteNode, conversionMap, assetPath);
								map[guid] = conversionMap;
							}
						}
					}

					if (conversionMap != null)
					{
						if (conversionMap.TryGetValue(id, out string newId))
						{
							// Create chunk
							string leftChunk = sb.ToString(chunkCursor, idChunkIndex - chunkCursor);
							string middleChunk = newId;
							chunkCursor = rightChunkIndex;

							chunks.Add(leftChunk);
							chunks.Add(middleChunk);
						}
					}
				}
			}
			else
			{
				//Debug.Log($"'{id}' : NO GUID");
			}

			nextIdIndex = sb.ExtIndexOf(fileIdText, nextIdIndex + 1, false);
		}

		// Add the last chunk
		if (chunkCursor != sb.Length)
			chunks.Add(sb.ToString(chunkCursor, sb.Length - chunkCursor));

		// Overwrite the file
		using (StreamWriter writer = new StreamWriter(File.Open(path, FileMode.Open, FileAccess.Write)))
		{
			writer.BaseStream.Seek(0, SeekOrigin.Begin);
			writer.BaseStream.SetLength(0);

			foreach (string chunk in chunks)
				writer.Write(chunk);
		}
	}

    static void PrefabPorting()
    {
		if (!EditorUtility.DisplayDialog("Prefab IDs", "Open exported ID folder of the other project. THIS DATA MUST HAVE BEEN EXPORTED PRIOR TO THE PORT", "Open", "Cancel"))
			return;

		string infoPath = EditorUtility.OpenFolderPanel("Prefab IDs", Application.dataPath, "Exported IDs");
		if (!Directory.Exists(infoPath))
		{
			EditorUtility.DisplayDialog("Error", "Could not find the exported folder", "Ok");
			return;
		}

		string[] files = IOUtils.GetAllFiles(targetAssetsPath);
		string[] assetPaths = files.Where(path => path.EndsWith(".prefab")).Concat(files.Where(path => FileUtils.ValidGuidFileExtensions.Contains(Path.GetExtension(path)) && !path.EndsWith(".prefab"))).ToArray();

		try
		{
			float i = 0;
			foreach (string asset in assetPaths)
			{
				EditorUtility.DisplayProgressBar("Porting", asset, i++ / assetPaths.Length);
				PortIDs(asset, infoPath);
			}
		}
		finally
		{
			EditorUtility.ClearProgressBar();
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}
    }

    [MenuItem("Tools/Universal Porter")]
    static void RunPorter()
    {
        // GET TARGET DIRECTORIES
        if (!EditorUtility.DisplayDialog("Setup", "Open the other project's Assets folder (etc. Tundra/Assets)", "Open", "Cancel"))
            return;

        otherAssetsPath = EditorUtility.OpenFolderPanel("Other project's assets", Path.GetDirectoryName(Application.dataPath), "Assets");
        if (!Directory.Exists(otherAssetsPath))
        {
            EditorUtility.DisplayDialog("Error", "Could not find the directory", "Ok");
            return;
        }
        otherProjectAssets = IOUtils.GetAllFiles(otherAssetsPath);

        if (!EditorUtility.DisplayDialog("Setup", "Open the folder which will be ported (etc. RudeLevelEditor/Assets/MyCustomLevel)", "Open", "Cancel"))
            return;

        targetAssetsPath = EditorUtility.OpenFolderPanel("Folder to port", Application.dataPath, "Custom");
        if (!Directory.Exists(targetAssetsPath))
        {
            EditorUtility.DisplayDialog("Error", "Could not find the directory", "Ok");
            return;
        }
        currentProjectAssets = IOUtils.GetAllFiles(Application.dataPath);

        // STEP 1
        if (EditorUtility.DisplayDialog("Step 1", "This step will link scripts from the other project to the target project", "Apply", "Skip"))
            ScriptPorting();

        // STEP 2
        if (EditorUtility.DisplayDialog("Step 2", "This step will fix the broken prefab overrides", "Apply", "Skip"))
            PrefabPorting();
    }
}
