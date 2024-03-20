#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

using ARFoundationWithOpenCVForUnity.UnityUtils.Helper;
using Aryzon;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARFoundationAryzon : MonoBehaviour, IAryzonEventHandler
{
    public void OnStartStereoscopicMode(AryzonModeEventArgs e)
    {
        stereomode = true ;
    }
    public void OnStopStereoscopicMode(AryzonModeEventArgs e)
    {
        stereomode = false;
    }
    public void OnStartAryzonMode(AryzonModeEventArgs e) { }
    public void OnStopAryzonMode(AryzonModeEventArgs e) { }

    //public ARCameraManager arCameraManager;
    float centerpnt;
    Vector2 center = new Vector2();

    bool stereomode = false;
    [SerializeField]
    public Camera mainCamera = default;

    /// <summary>
    /// The requested resolution dropdown.
    /// </summary>
    public Dropdown requestedResolutionDropdown;

    /// <summary>
    /// The requested resolution.
    /// </summary>
    public ResolutionPreset requestedResolution = ResolutionPreset._640x480;

   
    /// <summary>
    /// The texture.
    /// </summary>
    Texture2D texture;
    Texture2D overlaytex;

    public RawImage rawImage;
    public GameObject quad;
    public GameObject GO;
    Vector3 distfrmcam;
    public GameObject water;
    [SerializeField]
    ARRaycastManager m_RaycastManager;

    /// <summary>
    /// The webcam texture to mat helper.
    /// </summary>
    ARFoundationCameraToMatHelper webCamTextureToMatHelper;

    bool requestColor = false;
    Vector3 _mousePos;
    Vector2 _resPos = new Vector2();
    int boxesSeen = 0;
    Vector2 quadDimen = new Vector2();
    public int movAve = 5;
    double Height, Width;
    float imageHeight, imageWidth;
    List<double> heights = new List<double>();
    List<double> width = new List<double>();

    /// <summary>
    /// The rgb mat.
    /// </summary>
    Mat rgbMat;

    /// <summary>
    /// The threshold mat.
    /// </summary>
    Mat thresholdMat;

    /// <summary>
    /// The hsv mat.
    /// </summary>
    Mat hsvMat;

    Scalar hsvmin = new Scalar(0);
    Scalar hsvmax = new Scalar(0);
    Point max_center = new Point(0, 0);

    //TODO: adjust these in a calibration
    public float d = 4f; //diam in cm
    public float offset = 5f; // in ml
    public int sampleSize = 20; //pixels sqrd to sample for color
    public Scalar hsvrange = new Scalar(20, 50, 50);

    public int erode = 3;
    [Tooltip("dialate should be more than erode")]
    public int dialate = 8;

    Transform cam;

    // Use this for initialization
    void Start()
    {
        imageHeight = rawImage.transform.localScale.y;
        imageWidth = rawImage.transform.localScale.x;
        Debug.Log("updated width/height");
        webCamTextureToMatHelper = gameObject.GetComponent<ARFoundationCameraToMatHelper>();
        int width, height;
        Dimensions(requestedResolution, out width, out height);
        webCamTextureToMatHelper.requestedWidth = width;
        webCamTextureToMatHelper.requestedHeight = height;
        webCamTextureToMatHelper.Initialize();
        OrientationListener.orientationChanged += OnOrientationChanged;
        cam = FindObjectOfType<ARSessionOrigin>().camera.transform;
    }

    void OnOrientationChanged()
    {
        if (Screen.orientation == ScreenOrientation.LandscapeLeft || Screen.orientation == ScreenOrientation.LandscapeRight)
        {
            rawImage.transform.localScale = new Vector3(imageHeight, imageWidth, 1);

        }
        if (Screen.orientation == ScreenOrientation.Portrait || Screen.orientation == ScreenOrientation.PortraitUpsideDown)
        {
            rawImage.transform.localScale = new Vector3(imageWidth, imageHeight, 1);

        }
        Debug.Log("orientation changed: " + Screen.orientation);
    }

    /// <summary>
    /// Raises the webcam texture to mat helper initialized event.
    /// </summary>
    public void OnWebCamTextureToMatHelperInitialized()
    {
        Debug.Log("OnWebCamTextureToMatHelperInitialized");

        Mat webCamTextureMat = webCamTextureToMatHelper.GetMat();

        texture = new Texture2D(webCamTextureMat.cols(), webCamTextureMat.rows(), TextureFormat.RGBA32, false);
        overlaytex = new Texture2D(webCamTextureMat.cols(), webCamTextureMat.rows(), TextureFormat.RGBA32, false);
        Utils.fastMatToTexture2D(webCamTextureMat, texture);
        //quad.GetComponent<Renderer>().material.mainTexture = texture;

        //        quad.transform.localScale = new Vector3(webCamTextureMat.cols(), webCamTextureMat.rows(), 1);
        rawImage.texture = overlaytex;

        //Debug.Log(webCamTextureMat.cols());
        //rawImage.gameObject.transform.localScale = new Vector3(webCamTextureMat.cols()/100f, webCamTextureMat.rows()/100f, 1);
        Debug.Log("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);
        center.x = Screen.width / 2;
        center.y = Screen.height / 2;
       //var configurations = arCameraManager.GetConfigurations(Allocator.Temp);

       // foreach (XRCameraConfiguration config in configurations)
       // {
       //     Debug.Log("f");
       //     Debug.Log(config);
       // }

        rgbMat = new Mat(webCamTextureMat.rows(), webCamTextureMat.cols(), CvType.CV_8UC3);
        centerpnt = rgbMat.width() / 2;
        thresholdMat = new Mat();
        hsvMat = new Mat();

        float width = webCamTextureMat.width();
        float height = webCamTextureMat.height();

        float widthScale = (float)Screen.width / width;
        float heightScale = (float)Screen.height / height;
        if (widthScale < heightScale)
        {
            mainCamera.orthographicSize = (width * (float)Screen.height / (float)Screen.width) / 2;
        }
        else
        {
            mainCamera.orthographicSize = height / 2;
        }
        
        double fx;
        double fy;
        double cx;
        double cy;

        Mat camMatrix;

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API

            UnityEngine.XR.ARSubsystems.XRCameraIntrinsics cameraIntrinsics = webCamTextureToMatHelper.GetCameraIntrinsics();

            // Apply the rotate and flip properties of camera helper to the camera intrinsics.
            Vector2 fl = cameraIntrinsics.focalLength;
            Vector2 pp = cameraIntrinsics.principalPoint;
            Vector2Int r = cameraIntrinsics.resolution;

            Matrix4x4 tM = Matrix4x4.Translate(new Vector3(-r.x / 2, -r.y / 2, 0));
            pp = tM.MultiplyPoint3x4(pp);

            Matrix4x4 rotationAndFlipM = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, webCamTextureToMatHelper.rotate90Degree ? 90 : 0),
                new Vector3(webCamTextureToMatHelper.flipHorizontal ? -1 : 1, webCamTextureToMatHelper.flipVertical ? -1 : 1, 1));
            pp = rotationAndFlipM.MultiplyPoint3x4(pp);

            if (webCamTextureToMatHelper.rotate90Degree)
            {
                fl = new Vector2(fl.y, fl.x);
                r = new Vector2Int(r.y, r.x);
            }

            Matrix4x4 _tM = Matrix4x4.Translate(new Vector3(r.x / 2, r.y / 2, 0));
            pp = _tM.MultiplyPoint3x4(pp);

            cameraIntrinsics = new UnityEngine.XR.ARSubsystems.XRCameraIntrinsics(fl, pp, r);


            fx = cameraIntrinsics.focalLength.x;
            fy = cameraIntrinsics.focalLength.y;
            cx = cameraIntrinsics.principalPoint.x;
            cy = cameraIntrinsics.principalPoint.y;

            camMatrix = new Mat(3, 3, CvType.CV_64FC1);
            camMatrix.put(0, 0, fx);
            camMatrix.put(0, 1, 0);
            camMatrix.put(0, 2, cx);
            camMatrix.put(1, 0, 0);
            camMatrix.put(1, 1, fy);
            camMatrix.put(1, 2, cy);
            camMatrix.put(2, 0, 0);
            camMatrix.put(2, 1, 0);
            camMatrix.put(2, 2, 1.0f);

            Debug.Log("Created CameraParameters from the camera intrinsics to be populated if the camera supports intrinsics. \n" + camMatrix.dump() + "\n " + cameraIntrinsics.resolution);

#else // (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API

        int max_d = (int)Mathf.Max(width, height);
        fx = max_d;
        fy = max_d;
        cx = width / 2.0f;
        cy = height / 2.0f;

        camMatrix = new Mat(3, 3, CvType.CV_64FC1);
        camMatrix.put(0, 0, fx);
        camMatrix.put(0, 1, 0);
        camMatrix.put(0, 2, cx);
        camMatrix.put(1, 0, 0);
        camMatrix.put(1, 1, fy);
        camMatrix.put(1, 2, cy);
        camMatrix.put(2, 0, 0);
        camMatrix.put(2, 1, 0);
        camMatrix.put(2, 2, 1.0f);

        Debug.Log("Created a dummy CameraParameters. \n" + camMatrix.dump() + "\n " + width + " " + height);

#endif // (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API

    }

    /// <summary>
    /// Raises the webcam texture to mat helper disposed event.
    /// </summary>
    public void OnWebCamTextureToMatHelperDisposed()
    {
        Debug.Log("OnWebCamTextureToMatHelperDisposed");
        if (rgbMat != null)
            rgbMat.Dispose();
        if (thresholdMat != null)
            thresholdMat.Dispose();
        if (hsvMat != null)
            hsvMat.Dispose();
        if (texture != null)
        {
            Texture2D.Destroy(texture);
            texture = null;
        }

        if (overlaytex != null)
        {
            Texture2D.Destroy(overlaytex);
            overlaytex = null;
        }
    }


    /// <summary>
    /// Raises the webcam texture to mat helper error occurred event.
    /// </summary>
    /// <param name="errorCode">Error code.</param>
    public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode)
    {
        Debug.Log("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonUp(0))
        {
            requestColor = true;
            if (!stereomode)
                _mousePos = Input.mousePosition;
            if (stereomode)
                _mousePos = center;
            
            _resPos.x = map(Screen.width, texture.width, _mousePos.x);
            _resPos.y = map(Screen.height, texture.height, _mousePos.y);
            boxesSeen = 0;

        }

        if (webCamTextureToMatHelper.IsPlaying() && webCamTextureToMatHelper.DidUpdateThisFrame())
        {

            Mat rgbaMat = webCamTextureToMatHelper.GetMat();

            //Imgproc.putText (rgbaMat, "W:" + rgbaMat.width () + " H:" + rgbaMat.height () + " SO:" + Screen.orientation, new Point (5, rgbaMat.rows () - 10), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, new Scalar (255, 255, 255, 255), 2, Imgproc.LINE_AA, false);
            Imgproc.cvtColor(rgbaMat, rgbMat, Imgproc.COLOR_RGBA2RGB); //convert rgba to rgb




            if (requestColor)
            {

                map(Screen.width, texture.width, _mousePos.x);
                GetHSVVals(rgbMat, _resPos, out hsvmin, out hsvmax);

                //Debug.Log("hsvmin: " + hsvmin + "\n");
                //Debug.Log("hsvmax: " + hsvmax + "\n");
            }
            //first find blue objects
            Imgproc.cvtColor(rgbMat, hsvMat, Imgproc.COLOR_RGB2HSV); //convert to hsv

            Core.inRange(hsvMat, hsvmin, hsvmax, thresholdMat);

            Mat blank = new Mat(rgbaMat.rows(), rgbaMat.cols(), rgbaMat.type(), new Scalar(0, 0, 0, 0));

            //TODO: Map to the resolutionsx
            Imgproc.circle(blank, new Point((_resPos.x - centerpnt) / 1.2f + centerpnt, blank.height() - _resPos.y), 10, new Scalar(255, 255, 255, 255),-1);

            morphOps(thresholdMat); //removing noise

            trackFilteredObject(thresholdMat, hsvMat, blank);

            Utils.fastMatToTexture2D(blank, overlaytex);
            Utils.fastMatToTexture2D(rgbaMat, texture);
            requestColor = false;

        }
    }

    private void trackFilteredObject(Mat threshold, Mat HSV, Mat cameraFeed)
    {
        Mat temp = new Mat();
        List<MatOfPoint> contours = new List<MatOfPoint>();



        threshold.copyTo(temp);
        //these two vectors needed for output of findContours

        Mat hierarchy = new Mat();


        //find contours of filtered image using openCV findContours function

        Imgproc.findContours(temp, contours, hierarchy, Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);


        ////use moments method to find our filtered object
        ////TODO: decide how you are going to handle the contours here
        if (contours.Count > 0)
        {
            //int numObjects = hierarchy.rows();

            //                      Debug.Log("hierarchy " + hierarchy.ToString());

            //get max contours
            MatOfPoint maxCont = contours[0];
            double max = float.MinValue;

            foreach (MatOfPoint contour in contours)
            {
                if (Imgproc.contourArea(contour) > max)
                {
                    maxCont = contour;
                    max = Imgproc.contourArea(contour);
                }
            }


            //    //if number of objects greater than MAX_NUM_OBJECTS we have a noisy filter
            //    //TODO: probsbly finding max 2 contours instead here

            Moments moment = Imgproc.moments(maxCont);
            max_center.x = (int)(moment.get_m10() / max);
            max_center.y = (int)(moment.get_m01() / max);



            List<MatOfPoint> maxContours = new List<MatOfPoint>();
            MatOfPoint2f Cont = new MatOfPoint2f();

            maxContours.Add(maxCont);
            contours.Clear();
            contours.Add(maxCont);

            //draw object location on screen

            //go.transform.position = Camera.main.ScreenToWorldPoint(new Vector3((float)max_center.x, texture.height - (float)max_center.y, 60));
            Imgproc.drawContours(cameraFeed, contours, 0, new Scalar(0, 255, 0,255),20);
            Imgproc.circle(cameraFeed, max_center, 5, new Scalar(255, 0, 0, 255));

        }

        //else { Debug.Log("NO CONTOURS"); }



        if (contours.Count > 0)
        {
            boxesSeen++;
            //RotatedRect rect = Imgproc.minAreaRect(new MatOfPoint2f(contours[0].toArray()));
            OpenCVForUnity.CoreModule.Rect rect = Imgproc.boundingRect(new MatOfPoint(contours[0].toArray()));
            //Point[] rect_points = new Point[4];
            //rect.points(rect_points);
            ////               Imgproc.rectangle(cameraFeed, rect_points[0], rect_points[2], new Scalar(255, 0, 0), -1);
            //for (int j = 0; j < 4; j++)
            //{
            //    Imgproc.line(cameraFeed, rect_points[j], rect_points[(j + 1) % 4], new Scalar(0, 255, 0), 6);
            //}

            Imgproc.rectangle(cameraFeed, rect, new Scalar(255, 0, 0, 255), 15);

            //TODO: height and width are wrong. need to use a regular rect, not rotated rect.
            Height = rect.size().height;
            Width = rect.size().width;

            heights.Add(Height);
            width.Add(Width);

            if (requestColor)
            {
                Vector2 bottompnt = new Vector2(rect.x + rect.width / 2, texture.height - (rect.y + rect.height));
                bottompnt.x = map(texture.width, Screen.width,  bottompnt.x);
                bottompnt.y = map(texture.height, Screen.height,  bottompnt.y);
                PlaceGO(bottompnt);
            }
            if (boxesSeen < movAve)
            {
                Height = Height + (rect.size().height - Height) / (movAve + 1);
                Width = Width + (rect.size().width - Width) / (movAve + 1);
            }
            if (boxesSeen > movAve)
            {
                heights.RemoveAt(0);
                width.RemoveAt(0);
            }


            double scale = d / width.Average();

            // float angle = (Camera.main.ScreenToViewportPoint(new Vector3((float)rect_points[0].x, (float)rect_points[0].y, 0)).y - .5f)/.5f * Camera.main.fieldOfView/2;
            //don't need this bc tracks at the miniscus
            //float angle = (Camera.main.ScreenToViewportPoint(new Vector3((float)rect.tl().x, (float)rect.tl().y, 0)).y - .5f) / .5f * Camera.main.fieldOfView / 2;
            float angle = Vector3.Angle(Camera.main.ScreenPointToRay(new Vector3((float)rect.tl().x, (float)rect.tl().y - Screen.height / 2, 0)).direction, Camera.main.transform.forward);
            double dHeight = d * Math.Tan((angle) * Math.PI / 180);


            double aveH = heights.Average() * scale;// - dHeight;
            //Debug.Log("width = " + width.Average());
            //Debug.Log(aveH);
            //aveH = aveH / Math.Cos(angle * Math.PI / 180);
            //Debug.Log(aveH);
            //Debug.Log("height = " + aveH);
            double vol = Mathf.PI * d * d / 4 * aveH - offset;
            water.transform.localScale = new Vector3(water.transform.localScale.x, (float)aveH / 100, water.transform.localScale.z);

                        Debug.Log("vol = " + vol);
        }


    }

    void PlaceGO(Vector2 Screenpnt)
    {
        List<ARRaycastHit> m_Hits = new List<ARRaycastHit>();
        if(m_RaycastManager.Raycast(Screenpnt, m_Hits))
        {
            ARRaycastHit hit = m_Hits[0];
            //GO.transform.position = hit.pose.position + new Vector3(cam.forward.x, 0, cam.forward.z).normalized * d/200 ;
            //GO.transform.rotation = Quaternion.LookRotation(Camera.main.transform.position - hit.pose.position);
            Debug.Log("AR Raycast Hit!");
            distfrmcam = hit.pose.position - Camera.main.transform.position;
        }
    }

    void GetHSVVals(Mat rgb, Vector3 mousepos, out Scalar HSVmin, out Scalar HSVmax)
    {
        mousepos.x = (mousepos.x - centerpnt) / 1.2f + centerpnt;
        //Texture2D temp = new Texture2D(rgba.cols(), rgba.rows(), TextureFormat.RGBA32, false);
        Utils.matToTexture2D(rgb, texture, true, 0, true);
        Debug.Log(texture.width + " " + texture.height + " " + mousepos);
        Debug.Log(rgb.height() - mousepos.y);
        int x = (int)mousepos.x - sampleSize / 2;
        if (x + sampleSize / 2 > rgb.width())
            x = rgb.width() - sampleSize / 2;
        int y = (int)mousepos.y - sampleSize / 2;
        if (y + sampleSize / 2 > rgb.height())
            y = rgb.height() - sampleSize / 2;
        Color[] HSV = texture.GetPixels(x, y, sampleSize, sampleSize);

        Color HSVave = GetAveColor(HSV);

        Texture2D temp = new Texture2D(sampleSize, sampleSize);
        temp.SetPixels(HSV);
        Mat rgbam = new Mat(sampleSize, sampleSize, CvType.CV_8UC4);
        Utils.texture2DToMat(temp, rgbam);
        Mat rgbm = new Mat(sampleSize, sampleSize, CvType.CV_8UC3);
        Imgproc.cvtColor(rgbam, rgbm, Imgproc.COLOR_RGBA2RGB);

        //Debug.Log(texture.GetPixels((int)mousepos.x-50, (int)mousepos.y-50,100,100).Length);
        float[] trueHSV = new float[3];
        Color.RGBToHSV(HSVave, out trueHSV[0], out trueHSV[1], out trueHSV[2]);
        //Debug.Log(HSVave);
        double[] vals = new double[4] { Math.Floor((double)trueHSV[0] * 179), Math.Floor((double)trueHSV[1] * 255), Math.Floor((double)trueHSV[2] * 255), 0 };
        Scalar scaleHSV = new Scalar(vals);
        Debug.Log(scaleHSV);
        HSVmin = scaleHSV - hsvrange;
        for (int i = 0; i < 4; i++)
        {
            if (HSVmin.val[i] < 0)
                HSVmin.val[i] = 0;
        }

        // HSVmin.val[2] = 25;
        HSVmax = scaleHSV + hsvrange;
        for (int i = 0; i < 4; i++)
        {
            if (HSVmax.val[i] > 255)
                HSVmax.val[i] = 255;
        }
        // HSVmax.val[2] = 225;
        //Debug.Log(HSV.Length);


        // HSVmin.set(new double[] { 100, 100, 25 });
        // HSVmax.set(new double[] { 125, 225, 225 });

        //temp.SetPixels(HSV);
        //testTex.SetTexture("_MainTex", temp);
        Texture2D.Destroy(temp);
        rgbm.Dispose();
        rgbam.Dispose();
    }

    Color GetAveColor(Color[] patch)
    {
        float r, g, b, a;
        r = g = b = a = 0;
        float tot = patch.Length;
        for (int i = 0; i < tot; i++)
        {
            r += patch[i].r;
            g += patch[i].g;
            b += patch[i].b;
            a += patch[i].a;
        }
        return new Color(r / tot, g / tot, b / tot, a / tot);
    }

    /// <summary>
    /// Morphs the ops. This removes noise
    /// </summary>
    /// <param name="thresh">Thresh.</param>
    private void morphOps(Mat thresh)
    {
        //create structuring element that will be used to "dilate" and "erode" image.
        //the element chosen here is a 3px by 3px rectangle
        Mat erodeElement = Imgproc.getStructuringElement(Imgproc.MORPH_RECT, new Size(erode, erode));
        //dilate with larger element so make sure object is nicely visible
        Mat dilateElement = Imgproc.getStructuringElement(Imgproc.MORPH_RECT, new Size(dialate, dialate));

        Imgproc.erode(thresh, thresh, erodeElement);
        Imgproc.erode(thresh, thresh, erodeElement);

        Imgproc.dilate(thresh, thresh, dilateElement);
        Imgproc.dilate(thresh, thresh, dilateElement);
    }

    /// <summary>
    /// Raises the destroy event.
    /// </summary>
    void OnDestroy()
    {
        webCamTextureToMatHelper.Dispose();
    }

    /// <summary>
    /// Raises the back button click event.
    /// </summary>
    public void OnBackButtonClick()
    {
        SceneManager.LoadScene("ARFoundationWithOpenCVForUnityExample");
    }

    /// <summary>
    /// Raises the play button click event.
    /// </summary>
    public void OnPlayButtonClick()
    {
        webCamTextureToMatHelper.Play();
    }

    /// <summary>
    /// Raises the pause button click event.
    /// </summary>
    public void OnPauseButtonClick()
    {
        webCamTextureToMatHelper.Pause();
    }

    /// <summary>
    /// Raises the stop button click event.
    /// </summary>
    public void OnStopButtonClick()
    {
        webCamTextureToMatHelper.Stop();
    }

    /// <summary>
    /// Raises the change camera button click event.
    /// </summary>
    public void OnChangeCameraButtonClick()
    {
        webCamTextureToMatHelper.requestedIsFrontFacing = !webCamTextureToMatHelper.requestedIsFrontFacing;
    }

    /// <summary>
    /// Raises the requested resolution dropdown value changed event.
    /// </summary>
    public void OnRequestedResolutionDropdownValueChanged(int result)
    {
        if ((int)requestedResolution != result)
        {
            requestedResolution = (ResolutionPreset)result;

            int width, height;
            Dimensions(requestedResolution, out width, out height);

            webCamTextureToMatHelper.Initialize(width, height);
            Debug.Log("change res");
        }

    }


    

    public enum FPSPreset : int
    {
        _0 = 0,
        _1 = 1,
        _5 = 5,
        _10 = 10,
        _15 = 15,
        _30 = 30,
        _60 = 60,
    }

    public enum ResolutionPreset : byte
    {
        _2556x1179 = 0,
        _640x480,
        _1280x720,
        _1920x1080,
        _9999x9999,
    }

    public void AdjustImage(float width)
    {
        rawImage.transform.localScale = new Vector3(width, rawImage.transform.localScale.y, rawImage.transform.localScale.z);
        Debug.Log("imgwidth: " + width);
    }

    public void AdjustHeight(float height)
    {
        rawImage.transform.localScale = new Vector3(rawImage.transform.localScale.x, height, rawImage.transform.localScale.z);
        Debug.Log("imgheight: " + height);
    }


    private void Dimensions(ResolutionPreset preset, out int width, out int height)
    {
        switch (preset)
        {
            case ResolutionPreset._2556x1179:
                width = 2256;
                height = 1179;
                break;
            case ResolutionPreset._640x480:
                width = 1920;
                height = 1440;
                break;
            case ResolutionPreset._1280x720:
                width = 3840;
                height = 2160;
                break;
            case ResolutionPreset._1920x1080:
                width = 1920;
                height = 1080;
                break;
            case ResolutionPreset._9999x9999:
                width = 9999;
                height = 9999;
                break;
            default:
                width = height = 0;
                break;
        }
    }

    float map(float screen, float res, float mouse)
    {
        return mouse * res / screen;
    }
}

#endif

public sealed class ScalarAverage : Average<float>
{
    public override float Operation(float a, float b)
    {
        return (a + b) / 2;
    }

    public override float Operation(IEnumerable<float> values)
    {
        return values.Average();
    }
}