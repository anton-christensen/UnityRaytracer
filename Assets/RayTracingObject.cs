using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class RayTracingObject : MonoBehaviour
{
    public Color32 albedo;
    public Color32 specular;
    public Color32 emission;
    public float smoothness;
    public bool RandomMaterial = true;

    public RayTracer.Material material {
        get {
            return new RayTracer.Material() {
                albedo = ToVector3(albedo),
                specular = ToVector3(specular),
                emission = ToVector3(emission),
                smoothness = smoothness
            };
        }
    }

    private void OnEnable() {
        if(RandomMaterial) this.SetRandom();
        RayTracingMaster.RegisterObject(this);
    }
    private void OnDisable() {
        RayTracingMaster.UnregisterObject(this);
    }

    private void SetRandom() {
        // Albedo and specular color
        Color color = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.5f, 0.8f);
        bool metal = Random.value < 0.5f;
        albedo = ToColor32(metal ? Vector3.zero : new Vector3(color.r, color.g, color.b));
        specular = ToColor32(metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f);
        smoothness = Random.value;

        bool isEmitting = Random.value < 0.2;
        if (isEmitting) {
            Color emission = Random.ColorHSV(0, 1, 0, 1, 3.0f, 8.0f);
            emission = ToColor32(new Vector3(emission.r, emission.g, emission.b));
        } else {
            emission = ToColor32(new Vector3(0.0f, 0.0f, 0.0f));
        }
    }




    public static Vector3 ToVector3(Color32 c) {
        return new Vector3(c.r / 255.0f, c.g / 255.0f, c.b / 255.0f);
    }

    public static Color32 ToColor32(Vector3 v) {
        return new Color32((byte)(v.x * 255.0f), (byte)(v.y * 255.0f), (byte)(v.z * 255.0f), 255);
    }

}