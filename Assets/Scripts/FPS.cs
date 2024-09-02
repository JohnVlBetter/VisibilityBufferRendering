using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace pff.Homestead
{
    [ExecuteAlways]
    public class FPS : MonoBehaviour
    {
        private int _frame = 0;

        //帧间间隔
        private double gpuTotalTime;
        private double cpuTotalTime;
        private double cpuTime;
        private double gpuTime;
        private readonly int totalCount = 30;

        private void Awake() {
            Application.targetFrameRate = 600;
        }

        void Update()
        {
            if (Debug.isDebugBuild)
            {
                var cpu = Recorder.Get("CPU Total Frame Time");
                var gpu = Recorder.Get("FrameTime.GPU");
                gpuTotalTime += gpu.gpuElapsedNanoseconds * 1e-6;
                cpuTotalTime += cpu.elapsedNanoseconds * 1e-6;
            }
            else
            {
                cpuTotalTime += Time.deltaTime * 1000;
            }
            _frame++;
            if (_frame >= totalCount)
            {
                cpuTime = cpuTotalTime / totalCount;
                gpuTime = gpuTotalTime / totalCount;
                _frame = 0;
                gpuTotalTime = 0;
                cpuTotalTime = 0;
            }
        }
        private void OnGUI()
        {
            int h = 24;
            int x = Screen.width - 620;
            int y = Screen.height - h - 2;
            GUI.Box(new Rect(x, y, 600, h), "");
            GUI.skin.label.fontSize = 20;
            //GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            if (cpuTime < 17)
            {
                GUI.color = Color.green;
            }
            else if (cpuTime < 34)
            {
                GUI.color = Color.yellow;
            }
            else
            {
                GUI.color = Color.red;
            }
            GUI.Label(new Rect(x, y, 200, h), $" FPS:{1000 / cpuTime:F2}");
            GUI.Label(new Rect(x + 260, y, 200, h), string.Format("CPU:{0:N2}", cpuTime));

            if (gpuTime < 17)
            {
                GUI.color = Color.green;
            }
            else if (gpuTime < 34)
            {
                GUI.color = Color.yellow;
            }
            else
            {
                GUI.color = Color.red;
            }
            GUI.Label(new Rect(x + 130, y, 200, h), string.Format("GPU:{0:N2}", gpuTime));
            GUI.color = Color.white;

        }

    }
}