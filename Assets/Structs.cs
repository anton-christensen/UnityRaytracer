using UnityEngine;

namespace RayTracer {
    public struct Material {
        public Vector3 albedo;
        public Vector3 specular;
        public float smoothness;
        public Vector3 emission;
    };
}