﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Unity.SharingWithUNET;
using System;
using Previs;

public class MyUIManager : Singleton<MyUIManager>
{
    public enum ModelEditType
    {
        Move = 0,
        Rotate = 1,
        Scale = 2
    }

    public ModelEditType CurrentModelEditMode { get; private set; }

    private PrevisTag previsTag = null;

    // audio
    [SerializeField]
    private AudioClip clickButtonSound;
    private AudioSource clickButtonAudioSource;

    public void Start()
    {
        CurrentModelEditMode = ModelEditType.Move;

        EnableMainMenu(false);

        // audio
        if(clickButtonSound)
        {
            clickButtonAudioSource = GetComponent<AudioSource>();
            clickButtonAudioSource.clip = clickButtonSound;
            clickButtonAudioSource.playOnAwake = false;
            clickButtonAudioSource.spatialBlend = 1;
            clickButtonAudioSource.dopplerLevel = 0;
        }
    }

    public void EnableMainMenu(bool value = true)
    {
        GameObject appBar = GameObject.Find("AppBar");
        if (appBar)
        {
            if (value == true)
                appBar.GetComponent<HoloToolkit.Unity.UX.AppBar>().State = HoloToolkit.Unity.UX.AppBar.AppBarStateEnum.Default;
            else
                appBar.GetComponent<HoloToolkit.Unity.UX.AppBar>().State = HoloToolkit.Unity.UX.AppBar.AppBarStateEnum.Hidden;
        }

        var t = GameObject.Find("UIStatus");
        if (t)
        {
            t.GetComponent<MeshRenderer>().enabled = value;
        }
    }

    public void UpdateText(string str)
    {
        var t = GameObject.Find("UIStatus");
        if(t)
        {
            t.GetComponent<TextMesh>().text = str;
        }
    }

    public void PrevisModelLoaded(PrevisTag tag)
    {
        previsTag = tag;
        CurrentModelEditMode = ModelEditType.Move;
        if (previsTag.type == "mesh")
            UpdateText("mesh loaded, mode: move");
        else if(previsTag.type == "point")
            UpdateText("pointcloud loaded, mode: move");
    }

    public void PrevisModelUnloaded(string err)
    {
        if (err != "")
        {
            UpdateText(err);
            previsTag = null;
            return;
        }

        if (previsTag != null)
        {
            if (previsTag.type == "mesh")
                UpdateText("mesh unloaded");
            else if (previsTag.type == "point")
                UpdateText("pointcloud unloaded");
            previsTag = null;
        } 
    }

    public void MoveModel()
    {
        CurrentModelEditMode = ModelEditType.Move;
        UpdateText("mode: move");
    }

    public void RotateModel()
    {
        CurrentModelEditMode = ModelEditType.Rotate;
        UpdateText("mode: rotate");
    }

    public void ScaleModel()
    {
        CurrentModelEditMode = ModelEditType.Scale;
        UpdateText("mode: scale");
    }

    public void OnAppBarButtonClicked(string name)
    {
        Debug.Log("OnLoadButtonClicked " + name);
        if(clickButtonAudioSource != null)
        {
            Debug.Log("Play Sound");
            clickButtonAudioSource.Play();
        }

        switch (name) {
            case "Scan":
                ScanQR();
                break;

            case "Load":
                LoadTestModel();
                break;

            case "Move":
                CurrentModelEditMode = ModelEditType.Move;
                UpdateText("mode: move");
                break;

            case "Rotate":
                CurrentModelEditMode = ModelEditType.Rotate;
                UpdateText("mode: rotate");
                break;

            case "Scale":
                CurrentModelEditMode = ModelEditType.Scale;
                UpdateText("mode: scale");
                break;

            case "Unload":
                UnloadModel();
                break;

            default:
                break;
        }
    }

    private void LoadTestModel()
    {
        if (PlayerController.Instance == null || previsTag != null) return;

        Debug.Log("Start to load test model");
        PlayerController.Instance.StartLoadPrevisTag("4194b4");   // mesh heart
        //PlayerController.Instance.StartLoadPrevisTag("d3ef22");   // tikal point cloud
        //PlayerController.Instance.StartLoadPrevisTag("948a98");     // mesh baybridge
        //PlayerController.Instance.StartLoadPrevisTag("634b73");   //to test network download
    }

    private void ScanQR()
    {
        if (PlayerController.Instance == null || previsTag != null) return;

#if !UNITY_EDITOR
        UpdateText("scan QR for 60s");
    MediaFrameQrProcessing.Wrappers.ZXingQrCodeScanner.ScanFirstCameraForQrCode(
        result =>
        {
          UnityEngine.WSA.Application.InvokeOnAppThread(() =>
          {
            if(result != null)
            {
                UpdateText("found tag: " + result);
                if (PlayerController.Instance != null && previsTag == null)
                {
                    Debug.Log("Start to load model from tag: " + result);
                    PlayerController.Instance.StartLoadPrevisTag(result);
                }
            }   
            else
            {
                UpdateText("canceled - not found");
            }
          }, 
          false);
        },
        TimeSpan.FromSeconds(60)
    );
#else
        UpdateText("Scan function is not available");
#endif
    }

    public void UnloadModel()
    {
        if(PlayerController.Instance != null && previsTag != null)
        {
            PlayerController.Instance.UnloadModel();
        }
    }


}
