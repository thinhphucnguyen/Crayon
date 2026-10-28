using System;
using UnityEngine;

namespace RTCore
{
    public static class RTCameraRig
    {
        public static RTPackedCamera Pack(Camera cam, int width, int height)
        {
            if (cam.orthographic) throw new InvalidOperationException("RTCore needs a perspective camera.");

            Transform t = cam.transform;
            double halfH = Math.Tan(cam.fieldOfView * 0.5 * Math.PI / 180.0);   // fieldOfView is the vertical FOV
            return RTCameraMath.PackFromBasis(Arr(t.position), Arr(t.forward), Arr(t.right), Arr(t.up), halfH, width, height);
        }

        private static double[] Arr(Vector3 v) { return new double[] { v.x, v.y, v.z }; }
    }
}
