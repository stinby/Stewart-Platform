﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO.Ports;

public class Platform : MonoBehaviour
{
    [Header("Constants")]
    public float SCALE_INITIAL_HEIGHT = 100f; //  250
    public float SCALE_BASE_RADIUS = 100f; //140
    public float SCALE_PLATFORM_RADIUS = 100f;
    public float SCALE_HORN_LENGTH = 30f;
    public float SCALE_LEG_LENGTH = 105; // 270

    public float MAX_TRANSLATION = 50;
    public float MAX_ROTATION = Mathf.PI / 2f;

    [Space(20)]
    public float scale = 1;

    [Header("Component GameObjects")]
    public GameObject topPlate;
    public GameObject[] actuatorArms;
    public GameObject[] topPlateAttachmentPoints;
    public GameObject[] actuatorAttachmentPoints;

    [Header("UI")]
    public Slider posX;
    public Slider posY, posZ, rotX, rotY, rotZ; // Put on new line to avoid weird inspector issue with header
    public Text[] angleText;

    private Vector3 translation = new Vector3(), rotation = new Vector3(), initialHeight = new Vector3();
    private Vector3[] baseJoint = new Vector3[6], platformJoint = new Vector3[6], q = new Vector3[6], l = new Vector3[6], A = new Vector3[6];
    private float[] alpha = new float[6];
    private float baseRadius, platformRadius, hornLength, legLength;

    private readonly float[] baseAngles = new float[] {
   314.9f, 345.1f, 74.9f, 105.1f, 194.9f, 225.1f };

    private readonly float[] platformAngles = new float[]{
   322.9f, 337.1f, 82.9f, 97.1f, 202.9f, 217.1f};

    private readonly float[] beta = new float[]{
   -8*Mathf.PI/3, Mathf.PI/3, 0, -Mathf.PI, -4*Mathf.PI/3, -7*Mathf.PI/3};

    public string port;
    private SerialPort sp;

    // Use this for initialization
    void Start()
    {
        initialHeight = new Vector3(0, 0, scale * SCALE_INITIAL_HEIGHT);
        baseRadius = scale * SCALE_BASE_RADIUS;
        platformRadius = scale * SCALE_PLATFORM_RADIUS;
        hornLength = scale * SCALE_HORN_LENGTH;
        legLength = scale * SCALE_LEG_LENGTH;

        for (int i = 0; i < 6; i++)
        {
            float mx = baseRadius * Mathf.Cos(Mathf.Deg2Rad * baseAngles[i]);
            float my = baseRadius * Mathf.Sin(Mathf.Deg2Rad * baseAngles[i]);
            baseJoint[i] = new Vector3(mx, my, 0);
        }

        for (int i = 0; i < 6; i++)
        {
            float mx = platformRadius * Mathf.Cos(Mathf.Deg2Rad * platformAngles[i]);
            float my = platformRadius * Mathf.Sin(Mathf.Deg2Rad * platformAngles[i]);

            platformJoint[i] = new Vector3(mx, my, 0);
            q[i] = new Vector3(0, 0, 0);
            l[i] = new Vector3(0, 0, 0);
            A[i] = new Vector3(0, 0, 0);
        }
        calcQ();

        try
        {
            sp = new SerialPort(port, 9600);
            sp.Open();
            sp.ReadTimeout = 10;
        }
        catch (System.Exception e)
        {

        }
    }

    void FixedUpdate()
    {
        //Debug.DrawLine(Vector3.zero, new Vector3(5, 5, 0));

        // Adjust position and rotation based on slider values;
        topPlate.transform.position = new Vector3(posX.value, posY.value, posZ.value) / 2f;
        topPlate.transform.rotation = Quaternion.Euler(rotX.value * 90f, rotY.value * 90f, rotZ.value * 90f);
        translation = topPlate.transform.position * MAX_TRANSLATION;
        rotation = (new Vector3(rotX.value, rotY.value, rotZ.value)) * MAX_ROTATION;
        calcQ();
        calcAlpha();

        int direction = 1;
        for (int i = 0; i < 6; i++)
        {
            angleText[i].text = "" + Mathf.Rad2Deg * alpha[i];
            try
            {
                sp.Write(((int)(direction * Mathf.Rad2Deg * alpha[i] + 90)).ToString() + ",");
            }
            catch (System.Exception e)
            {

            }

            direction *= -1;

            Vector3 euler = actuatorArms[i].transform.localEulerAngles;
            euler.y = Mathf.Pow(-1, i + 1) * Mathf.Rad2Deg * alpha[i];
            actuatorArms[i].transform.localEulerAngles = euler;

            Debug.DrawLine(actuatorAttachmentPoints[i].transform.position, topPlateAttachmentPoints[i].transform.position);
        }
    }

    private void calcQ()
    {
        for (int i = 0; i < 6; i++)
        {
            // rotation
            q[i].x = Mathf.Cos(rotation.z) * Mathf.Cos(rotation.y) * platformJoint[i].x +
              (-Mathf.Sin(rotation.z) * Mathf.Cos(rotation.x) + Mathf.Cos(rotation.z) * Mathf.Sin(rotation.y) * Mathf.Sin(rotation.x)) * platformJoint[i].y +
              (Mathf.Sin(rotation.z) * Mathf.Sin(rotation.x) + Mathf.Cos(rotation.z) * Mathf.Sin(rotation.y) * Mathf.Cos(rotation.x)) * platformJoint[i].z;

            q[i].y = Mathf.Sin(rotation.z) * Mathf.Cos(rotation.y) * platformJoint[i].x +
              (Mathf.Cos(rotation.z) * Mathf.Cos(rotation.x) + Mathf.Sin(rotation.z) * Mathf.Sin(rotation.y) * Mathf.Sin(rotation.x)) * platformJoint[i].y +
              (-Mathf.Cos(rotation.z) * Mathf.Sin(rotation.x) + Mathf.Sin(rotation.z) * Mathf.Sin(rotation.y) * Mathf.Cos(rotation.x)) * platformJoint[i].z;

            q[i].z = -Mathf.Sin(rotation.y) * platformJoint[i].x +
              Mathf.Cos(rotation.y) * Mathf.Sin(rotation.x) * platformJoint[i].y +
              Mathf.Cos(rotation.y) * Mathf.Cos(rotation.x) * platformJoint[i].z;

            // translation
            q[i] += translation + initialHeight;
            l[i] = q[i] - baseJoint[i];
        }
    }

    private void calcAlpha()
    {
        for (int i = 0; i < 6; i++)
        {
            float L = l[i].sqrMagnitude - (legLength * legLength) + (hornLength * hornLength);
            float M = 2 * hornLength * (q[i].z - baseJoint[i].z);
            float N = 2 * hornLength * (Mathf.Cos(beta[i]) * (q[i].x - baseJoint[i].x) + Mathf.Sin(beta[i]) * (q[i].y - baseJoint[i].y));
            alpha[i] = Mathf.Asin(L / Mathf.Sqrt(M * M + N * N)) - Mathf.Atan2(N, M);

            A[i] = new Vector3(hornLength * Mathf.Cos(alpha[i]) * Mathf.Cos(beta[i]) + baseJoint[i].x,
            hornLength * Mathf.Cos(alpha[i]) * Mathf.Sin(beta[i]) + baseJoint[i].y,
            hornLength * Mathf.Sin(alpha[i]) + baseJoint[i].z);

            float xqxb = (q[i].x - baseJoint[i].x);
            float yqyb = (q[i].y - baseJoint[i].y);
            float h0 = Mathf.Sqrt((legLength * legLength) + (hornLength * hornLength) - (xqxb * xqxb) - (yqyb * yqyb)) - q[i].z;

            float L0 = 2 * hornLength * hornLength;
            float M0 = 2 * hornLength * (h0 + q[i].z);
            float a0 = Mathf.Asin(L0 / Mathf.Sqrt(M0 * M0 + N * N)) - Mathf.Atan2(N, M0);

            //println(i+":"+alpha[i]+"  h0:"+h0+"  a0:"+a0);
        }
    }
}
