using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class RayTracingMaster : MonoBehaviour {
    /* Structs */
    private struct MeshObject {
        public Matrix4x4 LocalToWorldMatrix;
        public int IndicesOffset;
        public int IndicesCount;
        public RayTracer.Material Material;
    };

    /* Static members */
    private static bool _meshObjectsNeedRebuilding;
    private static List<RayTracingObject> _rayTracingObjects = new List<RayTracingObject>();
    
    private static readonly int Sample = Shader.PropertyToID("_Sample");
    
    public static void RegisterObject(RayTracingObject obj) {
        _rayTracingObjects.Add(obj);
        _meshObjectsNeedRebuilding = true;
    }

    public static void UnregisterObject(RayTracingObject obj) {
        _rayTracingObjects.Remove(obj);
        _meshObjectsNeedRebuilding = true;
    }
    
    /* Non-static members */
    private ComputeBuffer _meshObjectBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;
    
    private RenderTexture _target;
    private RenderTexture _converged;
    private uint _currentSample;
    private Material _addMaterial;

    private Camera _camera;
    private float _lastFieldOfView;
    private List<Transform> _transformsToWatch = new List<Transform>();
    
    private List<MeshObject> _meshObjects = new List<MeshObject>();
    private List<Vector3> _vertices = new List<Vector3>();
    private List<int> _indices = new List<int>();
    
    /* Inspector-accessible members */
    public ComputeShader rayTracingShader;
    public Texture skyboxTexture;
    public Light directionalLight;
    [Range(2,32)]
    public int numBounces = 4;


    /* Event functions */
	private void Awake() {
	    _camera = GetComponent<Camera>();

        _transformsToWatch.Add(transform);
        _transformsToWatch.Add(directionalLight.transform);
	} 
    
    private void OnEnable() {
        _currentSample = 0;
    }

    private void OnDisable() {
        _meshObjectBuffer?.Release();
        _vertexBuffer?.Release();
        _indexBuffer?.Release();
    }
    
    private void Update() {
        if (_camera.fieldOfView != _lastFieldOfView) {
            _currentSample = 0;
            _lastFieldOfView = _camera.fieldOfView;
        }

        foreach (Transform t in _transformsToWatch) {
            if (t.hasChanged) {
                _currentSample = 0;
                t.hasChanged = false;
            }
        }

        rayTracingShader.SetInt("_numBounces", numBounces);
    }
    
    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        RebuildMeshObjectBuffers();
        SetShaderParameters();
        Render(destination);
    }
    
    private void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride) where T : struct {
        // Do we already have a compute buffer?
        if (buffer != null) {
            // If no data or buffer doesn't match the given criteria, release it
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride) {
                buffer.Release();
                buffer = null;
            }
        }
        
        if (data.Count != 0) {
            // If the buffer has been released or wasn't there to begin with, create it
            if (buffer == null) {
                buffer = new ComputeBuffer(data.Count, stride);
            }
            // Set data on the buffer
            buffer.SetData(data);
        }
    }
    
    private void SetComputeBuffer(string n, ComputeBuffer buffer) {
        if (buffer != null) {
            rayTracingShader.SetBuffer(0, n, buffer);
        }
    }
    
	private void SetShaderParameters() {
        Vector3 l = directionalLight.transform.forward;
        rayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, directionalLight.intensity));

        rayTracingShader.SetTexture(0, "_SkyboxTexture", skyboxTexture);
	    rayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
	    rayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        rayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        rayTracingShader.SetFloat("_Seed", Random.value);
        rayTracingShader.SetInt("_numBounces", numBounces);

        SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        SetComputeBuffer("_Vertices", _vertexBuffer);
        SetComputeBuffer("_Indices", _indexBuffer);
	}

    private void RebuildMeshObjectBuffers() {
        if (!_meshObjectsNeedRebuilding) {
            return;
        }

        _meshObjectsNeedRebuilding = false;
        _currentSample = 0;
        
        // Clear all lists
        _meshObjects.Clear();
        _vertices.Clear();
        _indices.Clear();
        
        // Loop over all objects and gather their data
        foreach (RayTracingObject obj in _rayTracingObjects) {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            
            // Add vertex data
            int firstVertex = _vertices.Count;
            _vertices.AddRange(mesh.vertices);
            
            // Add index data - if the vertex buffer wasn't empty before, the
            // indices need to be offset
            int firstIndex = _indices.Count;
            var indices = mesh.GetIndices(0);
            _indices.AddRange(indices.Select(index => index + firstVertex));
            
            _meshObjects.Add(new MeshObject() {
                LocalToWorldMatrix = obj.transform.localToWorldMatrix,
                IndicesOffset = firstIndex,
                IndicesCount = indices.Length,
                Material = obj.material
            });
        }
        
        CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, 112);
        CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        CreateComputeBuffer(ref _indexBuffer, _indices, 4);
    }
    
    private void Render(RenderTexture destination) {
        // Make sure we have a current render target
        InitRenderTexture();

        // Set the target and dispatch the compute shader
        rayTracingShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 16.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 16.0f);
        rayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        // Blit the result texture to the screen
        if (_addMaterial == null) {
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        }
        _addMaterial.SetFloat(Sample, _currentSample);

        Graphics.Blit(_target, _converged, _addMaterial);
        Graphics.Blit(_converged, destination);
        _currentSample++;
    }

    private void InitRenderTexture() {
        if (_target != null && _target.width == Screen.width && _target.height == Screen.height) {
            return;
        }
        
        // Release render texture if we already have one
        if (_target != null) {
            _target.Release();
            _converged.Release();
        }

        // Reset current sample
        _currentSample = 0;

        // Get a render target for Ray Tracing
        _target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear) {enableRandomWrite = true};
        _target.Create();

        _converged = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear) {enableRandomWrite = true};
        _converged.Create();
    }
}


