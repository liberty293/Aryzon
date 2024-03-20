using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class tester : MonoBehaviour
{
    public Vector2[] depth1;
    Camera cam;
    public Transform cube, top, bot, below;
    Vector2 screenpt, topscreen, botscreen;
    float center;

    void Start()
    {
        cam = GetComponent<Camera>();
        center = Screen.width / 2;
        Debug.Log(screenpt);

        Matrix4x4 m = Camera.main.projectionMatrix;
        Vector3 p = m.MultiplyPoint(new Vector3(0, 340, 4));
        cube.position = p;
    }

    void Update()
    {
        topscreen = cam.WorldToScreenPoint(top.position);
        topscreen = new Vector2(center, Screen.height / 2);
        screenpt = cam.WorldToScreenPoint(top.position);
        screenpt = new Vector2(center, screenpt.y);
        Ray ray = cam.ScreenPointToRay(screenpt);
        Ray raytop = cam.ScreenPointToRay(new Vector3(center,topscreen.y));
        Debug.DrawRay(ray.origin, ray.direction * 10, Color.red);
        Debug.DrawRay(raytop.origin, raytop.direction * 10, Color.blue);
        //float angle = Vector3.SignedAngle(raytop.direction, ray.direction, Vector3.right);
        float angle = Vector3.SignedAngle(Vector3.up, -ray.direction, Vector3.right);
        //float height = cam.ScreenToWorldPoint(new Vector3(center.position.x, bot.position.y, 4) - cam.ScreenToWorldPoint(new Vector3(below.position.x, below.position.y, 4);

        Debug.Log(cam.WorldToScreenPoint(top.position) - cam.WorldToScreenPoint(bot.position));
    }

}
