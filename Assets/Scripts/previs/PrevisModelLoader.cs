﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.SharingWithUNET;
using System.IO;
using System.IO.Compression;
using Previs;
using Dummiesman;
using HoloToolkit.Unity;
using UnityEngine.Rendering;
/*
#if !UNITY_EDITOR
using Ionic.Zip;
#endif
*/

public class PrevisModelLoader : MonoBehaviour
{
    public string previsTag = "";
    public Material defaultMaterial;
    public GameObject directionalIndicatorPrefab;

    public class MeshProperties
    {
        public MeshProperties(Color col, Vector3 pos, string meshName)
        {
            baseColour = col;
            originalPosition = pos;
            name = meshName;
        }
        public Color baseColour;
        public Vector3 originalPosition;
        public string name;
    }
    public Dictionary<string, MeshProperties> g_meshProperties = new Dictionary<string, MeshProperties>();


    private GameObject previsGroup = null;
    private string localDataFolder = string.Empty;

    public void Start()
    {
        LoadPrevisData();
    }

    private void LoadPrevisData()
    {
        if (previsTag == "") return; // or load default tag

        // 1. get json data from web
        // DEBUG: load json file from storage
        var jsonFileName = Path.Combine(Application.streamingAssetsPath, previsTag);
        jsonFileName = Path.Combine(jsonFileName, "info.json");
        Debug.Log("info json : " + jsonFileName);
        string jsonText = GetTextFileContent(jsonFileName);

        // 2. parse json data for the tag
        PrevisTag prevTag = JsonUtility.FromJson<PrevisTag>(jsonText);
        Debug.Log("Tag: " + prevTag.tag);
        Debug.Log("Type: " + prevTag.type);

        // 3. download processed data (zip file)

        // 4. create directory to store uncompressed data
        localDataFolder = Application.persistentDataPath + "/" + previsTag;
        Debug.Log("local data folder: " + localDataFolder);
        if(MyUIManager.Instance)
            MyUIManager.Instance.UpdateText(localDataFolder);
        Directory.CreateDirectory(localDataFolder);

        // 5. uncompress and load models
        // launch the mesh loader function as a coroutine so that the program will be semi-interactive while loading meshes :)
        // StartCoroutine(loadMeshes());
        if (prevTag.type == "mesh")
        {
            string meshParamsFile = Path.Combine(localDataFolder, "mesh.json");
            bool fileAvailable = File.Exists(meshParamsFile);
            if (fileAvailable == false)
            {
                Debug.Log("Unzip mesh data...");
                string zipFileName = Application.streamingAssetsPath + "/" + previsTag + "/mesh_processed.zip";
                zipFileName = zipFileName.Replace("\\", "/");

                new MyUnityHelpers().ExtractZipFile(zipFileName, localDataFolder);
            }

            Debug.Log("Loading a mesh...");
            StartCoroutine(fetchPrevisMesh(prevTag));
        }
        else if (prevTag.type == "point")
        {
            string pointCloudFile = localDataFolder + "/potree/cloud.js";
            pointCloudFile = pointCloudFile.Replace("\\", "/");

            bool fileAvailable = File.Exists(pointCloudFile);
            if (fileAvailable == false)
            {
                Debug.Log("Unzip point data...");
                string zipFileName = Application.streamingAssetsPath + "/" + previsTag + "/point_processed.zip";
                zipFileName = zipFileName.Replace("\\", "/");

                new MyUnityHelpers().ExtractZipFile(zipFileName, localDataFolder);
            }

            Debug.Log("Loading a point...");
            StartCoroutine(fetchPrevisPointCloud(prevTag));
            return;
        }
        else
        {
            Debug.Log("Error: invalid data type");
            return;
        }

    }

    string GetTextFileContent(string filename)
    {
        StreamReader reader = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read));
        string text = reader.ReadToEnd();
        reader.Dispose();
        return text;
    }

    public void AddMeshProperties(string meshname, Color colour, Vector3 position, string description)
    {
        g_meshProperties.Add(meshname, new MeshProperties(colour, position, description));
    }

    IEnumerator loadSampleMesh()
    {
        // load from Assets folder for testing, but not necessary
        string targetPath = Application.streamingAssetsPath + "/gnome/gnome1c.obj";

        GameObject parentObj = new OBJLoader().Load(targetPath);
        parentObj.name = "GNOME";
        parentObj.transform.parent = this.transform;

        yield return null;
    }

    IEnumerator fetchPrevisMesh(PrevisTag prevTag)
    {
        List<string> objectNames = new List<string>();
        List<string> OBJNames = new List<string>();

        // previs object holder
        previsGroup = new GameObject();
        previsGroup.name = prevTag.tag;
        previsGroup.transform.parent = this.transform;

        string meshParamsFile = Application.streamingAssetsPath + "/" + previsTag + "/mesh.json";
        meshParamsFile = meshParamsFile.Replace("\\", "/");
        string meshParams = GetTextFileContent(meshParamsFile);

        PrevisSceneParams meshParamsJson = JsonUtility.FromJson<PrevisSceneParams>(meshParams);

        if (meshParamsJson != null)
        {
            for (int pmpIndex = 0; pmpIndex < meshParamsJson.objects.Length; pmpIndex++)
            //for (int pmpIndex = 0; pmpIndex < 2; pmpIndex++)
            {
                PrevisMeshParamsNew pmp = meshParamsJson.objects[pmpIndex];
                //Debug.Log(pmp);
                GameObject groupParentNode = new GameObject();
                groupParentNode.name = pmp.name;
                groupParentNode.transform.parent = previsGroup.transform;
                if(prevTag.tag != "948a98") // TODO: ignore for baybride model for now. need a better way to enable/disable this
                    groupParentNode.AddComponent<Interactible>();
                AddMeshProperties(pmp.name, new Color(pmp.colour[0] / 255.0f, pmp.colour[1] / 255.0f, pmp.colour[2] / 255.0f), Vector3.zero, pmp.name);

                for (int pmgIndex = 0; pmgIndex < pmp.objects.Length; pmgIndex++)
                {
                    PrevisMeshGroupNew pmg = pmp.objects[pmgIndex];

                    // string targetPath = Application.dataPath + "/../" + folderName + "/" + OBJName;
                    string targetPath = localDataFolder + "/" + pmp.name + "/" + pmg.obj;
                    targetPath = targetPath.Replace("\\", "/");
                    if (!File.Exists(targetPath))
                    {
                        Debug.Log(targetPath + " is not exist!");
                        continue;
                    }

                    // FIXME: this may be overkill, but need to skip sending any non-OBJ files to the OBJ loader
                    if (Path.GetExtension(targetPath).ToUpper() != ".OBJ")
                    {
                        continue;
                    }

                    GameObject meshModel = new OBJLoader().Load(targetPath);
                    meshModel.transform.parent = groupParentNode.transform;
                    meshModel.name = pmg.obj;
                    meshModel.transform.localPosition = Vector3.zero;
                    ObjLoaded(pmp.name, meshModel);

                    yield return null;
                }
            }

            AllObjectsLoaded(prevTag);

        }

        yield return null;
    }


    Bounds GetGameObjectBound(GameObject g)
    {
        var b = new Bounds(g.transform.position, Vector3.zero);
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
        {
            b.Encapsulate(r.bounds);
        }
        return b;
    }

    void ObjLoaded(string name, GameObject target)
    {
        // update material for object here
        MeshProperties prop = g_meshProperties[name];
        foreach (MeshRenderer mr in target.GetComponentsInChildren<MeshRenderer>())
        {
            mr.material.color = prop.baseColour;
            // disable unused features
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // create mesh collisder
        foreach (Transform child in target.transform)
        {
            GameObject go = child.gameObject;
            Mesh m = (go.GetComponent(typeof(MeshFilter)) as MeshFilter).mesh;
            if (m)
            {
                MeshCollider mc = go.AddComponent<MeshCollider>() as MeshCollider;
                mc.sharedMesh = m;
            }
        }
    }

    void AllObjectsLoaded(PrevisTag prevTag, float defaultScale = 0.5f)
    {
        Debug.Log("Finished loading");
        if (previsGroup)
        {
            Vector3 center = GetGameObjectBound(previsGroup).center;
            Vector3 extends = GetGameObjectBound(previsGroup).extents;
            Debug.Log("center: " + center.ToString() + " extend: " + extends.ToString());
            float maxExtend = Mathf.Max(extends.x, extends.y, extends.z);
            float scale = defaultScale / maxExtend;
            UpdateObjectTransform(previsGroup, Vector3.zero, new Vector3(scale, scale, scale));
            previsGroup.transform.localPosition = scale * (-1 * center);

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.UpdateMovementOffset(new Vector3(0, scale * extends.y, 0));
            }

            // indicator
            //GameObject indicator = GameObject.FindGameObjectWithTag("DirectionalIndicator");
            if (directionalIndicatorPrefab != null)
            {
                GameObject indicator = Instantiate(directionalIndicatorPrefab, Vector3.zero, Quaternion.identity);
                if (indicator)
                {
                    indicator.transform.parent = this.transform;
                    indicator.transform.localPosition = Vector3.zero;
                    indicator.GetComponent<DirectionIndicator>().Cursor = GameObject.Find("Cursor");
                    indicator.GetComponent<DirectionIndicator>().enabled = true;
                }
            }

            //model loaded
            if(MyUIManager.Instance)
                MyUIManager.Instance.PrevisModelLoaded(prevTag);
        }
    }

    // === POINT CLOUD ===
    void UpdateObjectTransform(GameObject gameObject, Vector3 position, Vector3 scale)
    {
        gameObject.transform.localPosition = position;
        gameObject.transform.localScale = scale;
        /*
        foreach (Transform child in gameObject.transform)
        {
            GameObject c = child.gameObject;
            c.transform.localPosition = Vector3.zero;
        }
        */
    }

    IEnumerator fetchPrevisPointCloud(PrevisTag prevTag)
    {
        string previsTag = prevTag.tag;

        previsGroup = new GameObject();
        previsGroup.name = prevTag.tag;
        previsGroup.transform.parent = this.transform;

        string cloudPath = localDataFolder + "/potree/";
        cloudPath = cloudPath.Replace("\\", "/");
        new PrevisPotreeHelper().LoadPointCloud(cloudPath, previsGroup, 3, 6);

        //Vector3 extends = GetGameObjectBound(previsGroup).extents;
        //Debug.Log("center: " + GetGameObjectBound(previsGroup).center.ToString());
        //Debug.Log("extend: " + extends.ToString());

        AllObjectsLoaded(prevTag, 1.5f);

        yield return null;
    }

}