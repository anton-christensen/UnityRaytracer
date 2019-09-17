using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class RayTracingObject : MonoBehaviour
{
    public Color color = Color.white;
    [Range(0,1)]
    public float specular = 0;
    [Range(0,1)]
    public float emission = 0;
    [Range(0,1)]
    public float smoothness = 0;
    public bool RandomMaterial = true;

    public float albedo { 
        get {return (1.0f - specular);} 
        set {specular = (1.0f-value);}
    }

    public RayTracer.Material material {
        get { 
            return new RayTracer.Material() {
                albedo = ToVector3(color)*albedo,
                specular = ToVector3(color)*specular,
                emission = ToVector3(color)*emission,
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
        color = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.5f, 0.8f);
        specular = Random.value;
        smoothness = Random.value;

        bool isEmitting = Random.value < 0.2f;
        if (isEmitting) {
            emission = Random.Range(0.3f, 0.8f);
        } else {
            emission = 0;
        }
    }

    public static Vector3 ToVector3(Color c) {
        return new Vector3(c.r, c.g, c.b);
    }

    public static Color ToColor(Vector3 v) {
        return new Color(v.x, v.y, v.z);
    }

}