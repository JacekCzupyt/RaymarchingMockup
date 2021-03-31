using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraScript : MonoBehaviour
{
    public ComputeShader RayMarchingShader;

    private RenderTexture _target;
    private RenderTexture _converged;
    uint pass = 0;
    private Material _addMaterial;

    private Camera cam;

    public Texture SkyboxTexture;

    public GameObject TrackedSpheres;
    public GameObject TrackedBoxes;

    public Light DirectionalLight;
    
    public float DirectionalLightAngle = 20;

    public bool DynamicShadows;

    public float MinimumDistance = 0.0001f;
    public float BreakeOffDistance = 10000;
    public int ItterationLimit = 50;
    public float Blendingcoefficient = 1;
    public int RecursionLimit = 0;

    private ComputeBuffer _SphereBuffer;
    private ComputeBuffer _BoxBuffer;
    private ComputeBuffer _DistanceBuffer;

    public Vector4 color;

    struct Sphere
    {
        Vector3 origin;
        float radius;
        Vector4 color;
        public Sphere(Vector3 org, float r, Vector4 c)
        {
            origin = org;
            radius = r;
            color = c;
        }
    }

    struct Box
    {
        Vector3 origin;
        Vector3 dimentions;
        Vector4 color;
        public Box(Vector3 org, Vector3 dim, Vector4 c)
        {
            origin = org;
            dimentions = dim;
            color = c;
        }
    }

    struct MengerSponge
    {
        Vector3 origin;
        Vector3 dimentions;
        Vector4 color;
        int iterationCount;
        public MengerSponge(Vector3 org, Vector3 dim, Vector4 c, int iterations)
        {
            origin = org;
            dimentions = dim;
            color = c;
            iterationCount = iterations;
        }
    }

    private void OnEnable()
    {
        UpdateBuffers();
    }

    private void UpdateBuffers()
    {
        //List<Sphere> Spheres = new List<Sphere>();
        //foreach (Transform TrackedSphere in TrackedSpheres.transform)
        //{
        //    Spheres.Add(new Sphere(TrackedSphere.transform.position, TrackedSphere.transform.localScale.x, new Vector4(0.2f, 0.2f, 1f, 1)));
        //}
        //_SphereBuffer = new ComputeBuffer(Spheres.Count, 32);
        //_SphereBuffer.SetData(Spheres);

        //List<Box> Boxes = new List<Box>();
        //foreach (Transform TrackedBox in TrackedBoxes.transform)
        //{
        //    Boxes.Add(new Box(TrackedBox.transform.position, TrackedBox.transform.localScale, new Vector4(1f, 0.2f, 0.2f, 1)));
        //}
        //_BoxBuffer = new ComputeBuffer(Boxes.Count, 40);
        //_BoxBuffer.SetData(Boxes);

        List<MengerSponge> Boxes = new List<MengerSponge>();
        foreach (Transform TrackedBox in TrackedBoxes.transform)
        {
            Boxes.Add(new MengerSponge(TrackedBox.transform.position, TrackedBox.transform.localScale, new Vector4(0, 1, 0, 1), RecursionLimit));
        }
        _BoxBuffer = new ComputeBuffer(Boxes.Count, 44);
        _BoxBuffer.SetData(Boxes);

        _DistanceBuffer = new ComputeBuffer(1, 4);
        List<float> tem = new List<float>() { 0 };
        _DistanceBuffer.SetData(tem);
    }

    private void OnDisable()
    {
        if (_SphereBuffer != null)
            _SphereBuffer.Release();
        if (_BoxBuffer != null)
            _BoxBuffer.Release();
        if (_DistanceBuffer != null)
            _DistanceBuffer.Release();
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void SetShaderParameters()
    {
        RayMarchingShader.SetFloat("_MinimumDistance", MinimumDistance);
        RayMarchingShader.SetFloat("_BreakeOffDistance", BreakeOffDistance);
        RayMarchingShader.SetInt("_ItterationLimit", ItterationLimit);
        RayMarchingShader.SetVector("_DirectionalLight", DirectionalLight.transform.forward * DirectionalLight.intensity);
        RayMarchingShader.SetFloat("_DirectionalLightAngle", DirectionalLightAngle);
        RayMarchingShader.SetFloat("_Blendingcoefficient", Blendingcoefficient);


        //RayMarchingShader.SetFloat("_RecursionDistance", RecursionDistance);
        UpdateBuffers();
        //RayMarchingShader.SetBuffer(0, "_Spheres", _SphereBuffer);
        //RayMarchingShader.SetBuffer(0, "_Boxes", _BoxBuffer);
        RayMarchingShader.SetBuffer(0, "_Sponges", _BoxBuffer);
        RayMarchingShader.SetBuffer(0, "_DistanceBuffer", _DistanceBuffer);

        RayMarchingShader.SetMatrix("_CameraToWorld", cam.cameraToWorldMatrix);
        RayMarchingShader.SetMatrix("_CameraInverseProjection", cam.projectionMatrix.inverse);
        RayMarchingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        if(pass==0)
            RayMarchingShader.SetVector("_PixelOffset", new Vector2(0.5f, 0.5f));
        else
            RayMarchingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        RayMarchingShader.SetBool("_DynamicShadows", DynamicShadows);

        
    }



    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        // Make sure we have a current render target
        InitRenderTexture();
        // Set the target and dispatch the compute shader
        RayMarchingShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayMarchingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        float[] tem = new float[1];
        _DistanceBuffer.GetData(tem);
        GetComponent<FreeCam>().movementSpeed = 3 * tem[0];
        GetComponent<FreeCam>().fastMovementSpeed = 10 * tem[0];

        // Blit the result texture to the screen
        if (_addMaterial == null)
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        _addMaterial.SetFloat("_Sample", pass);
        Graphics.Blit(_target, destination, _addMaterial);
        pass++;



        //Graphics.Blit(_target, _converged, );
        //Graphics.Blit(_converged, destination);


        
    }

    private void InitRenderTexture()
    {
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            // Release render texture if we already have one
            if (_target != null)
                _target.Release();
            // Get a render target for Ray Tracing
            _target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
        }
    }

    private void Update()
    {
        Debug.Log(cam.projectionMatrix);
        Debug.Log(cam.projectionMatrix.inverse);

        if (transform.hasChanged)
        {
            Debug.Log(transform);
            ResetCamera(transform);
            return;
        }
        foreach (Transform t in TrackedBoxes.transform)
        {
            if(t.hasChanged)
            {
                ResetCamera(t);
            }
        }
        foreach (Transform t in TrackedSpheres.transform)
        {
            if (t.hasChanged)
            {
                ResetCamera(t);
            }
        }
    }

    private void ResetCamera(Transform t)
    {
        pass = 0;
        t.hasChanged = false;
    }
}
