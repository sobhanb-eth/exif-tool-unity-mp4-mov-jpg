using UnityEngine;
using System.Collections;
using System.IO;
using UnityEngine.Networking;
using UnityEngine.UI;
using VideoLib;
using ExifLib;
using UnityEngine.Video;

public class ReadEXIF : MonoBehaviour
{
    public RawImage imageHolder; // For image preview
    public Text ExifData;

    // For Video
    public RawImage videoHolder; // For video preview
    public Text MediaData;
    public InputField newMediaFileInputField;
    public Button newMediaFileButton;
    public Button videoRotateButton; // Rotation button for video

    public InputField newImageFileInputField;
    public Button newImageFileButton;
    public Button rotateButton; // Rotation button for image

    public bool usePixelRotation = true; // Checkbox to toggle rotation method

    private Texture2D texture = null;
    private Texture2D newTexture;
    private string imagePath;
    private string mediaPath;
    private string orientationString;

    private float videoRotationAngle = 0f;
    private float imageRotationAngle = 0f;

    private Material videoDefaultMaterial;
    private Material imageDefaultMaterial;
    private Material videoRotationMaterial;
    private Material imageRotationMaterial;

    void Awake()
    {
        newImageFileButton.onClick.AddListener(newImageFile);
        rotateButton.onClick.AddListener(Rotate90Clockwise);

        newMediaFileButton.onClick.AddListener(LoadMediaFile);
        videoRotateButton.onClick.AddListener(RotateVideo);

        // Store the default materials
        Shader defaultShader = Shader.Find("Universal Render Pipeline/Unlit");
        videoDefaultMaterial = new Material(defaultShader);
        imageDefaultMaterial = new Material(defaultShader);

        // Create materials with the rotation shader
        Shader rotationShader = Shader.Find("Custom/RotateTextureURP");
        videoRotationMaterial = new Material(rotationShader);
        imageRotationMaterial = new Material(rotationShader);

        // Set initial materials based on usePixelRotation
        UpdateMaterials();

        Debug.Log("Persistent path = " + Application.persistentDataPath);
    }

    void OnValidate()
    {
        if (videoHolder != null && imageHolder != null)
        {
            UpdateMaterials();
        }
    }

    void UpdateMaterials()
    {
        if (usePixelRotation)
        {
            // Use rotation materials
            videoHolder.material = videoRotationMaterial;
            imageHolder.material = imageRotationMaterial;
        }
        else
        {
            // Use default materials
            videoHolder.material = videoDefaultMaterial;
            imageHolder.material = imageDefaultMaterial;
        }
    }

    public void LoadMediaFile()
    {
        mediaPath = newMediaFileInputField.text;
        StartCoroutine(LoadMedia());
    }

    IEnumerator LoadMedia()
    {
        UnityWebRequest www = UnityWebRequest.Get(mediaPath);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            byte[] results = www.downloadHandler.data;
            string extension = Path.GetExtension(mediaPath).ToLower();

            if (extension == ".jpg" || extension == ".jpeg" || extension == ".png")
            {
                // Handle image files
                HandleImage(results);
            }
            else if (extension == ".mp4" || extension == ".mov" || extension == ".avi")
            {
                // Handle video files
                HandleVideo(results);
            }
            else
            {
                Debug.Log("Unsupported file type.");
            }
        }
    }

    void HandleVideo(byte[] data)
    {
        VideoInfo videoInfo = VideoMetadataReader.ReadVideo(data, Path.GetFileName(mediaPath));

        MediaData.text = "<b>Video Data:</b>" + "<color=white>";
        MediaData.text += "\n" + "FileName: " + videoInfo.FileName;
        MediaData.text += "\n" + "Width: " + videoInfo.Width + " pixels";
        MediaData.text += "\n" + "Height: " + videoInfo.Height + " pixels";
        MediaData.text += "\n" + "Rotation: " + videoInfo.Rotation + " degrees";
        MediaData.text += "</color>";

        // Set initial rotation based on metadata
        videoRotationAngle = videoInfo.Rotation % 360f;

        // Play the video in the preview panel
        StartCoroutine(PlayVideo(data));
    }

    IEnumerator PlayVideo(byte[] data)
    {
        // Save the video data to a temporary file
        string tempVideoPath = Path.Combine(Application.temporaryCachePath, "tempVideo.mp4");

        File.WriteAllBytes(tempVideoPath, data);

        VideoPlayer videoPlayer = videoHolder.GetComponent<VideoPlayer>();

        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = tempVideoPath;

        // Create a RenderTexture for the video
        RenderTexture renderTexture = new RenderTexture(1920, 1080, 0); // Adjust dimensions as needed

        videoPlayer.targetTexture = renderTexture;

        // Assign the RenderTexture to the RawImage
        videoHolder.texture = renderTexture;

        videoPlayer.Prepare();

        // Wait until the video is prepared
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }

        if (usePixelRotation)
        {
            // Set initial rotation in the shader
            videoRotationMaterial.SetFloat("_Rotation", videoRotationAngle);
        }
        else
        {
            // Reset RectTransform rotation
            videoHolder.rectTransform.localEulerAngles = Vector3.zero;
            // Apply initial rotation to RectTransform
            videoHolder.rectTransform.localEulerAngles = new Vector3(0, 0, -videoRotationAngle);
            // Adjust the size after rotation
            videoHolder.SizeToParent();
        }

        // Adjust the size
        videoHolder.SizeToParent();

        // Play the video
        videoPlayer.Play();
    }

    public void RotateVideo()
    {
        // Rotate the video by 90 degrees
        videoRotationAngle += 90f;
        if (videoRotationAngle >= 360f)
        {
            videoRotationAngle -= 360f;
        }

        if (usePixelRotation)
        {
            // Apply the rotation to the shader
            videoRotationMaterial.SetFloat("_Rotation", videoRotationAngle);
        }
        else
        {
            // Reset RectTransform rotation
            videoHolder.rectTransform.localEulerAngles = Vector3.zero;
            // Apply rotation to the RectTransform
            videoHolder.rectTransform.localEulerAngles = new Vector3(0, 0, -videoRotationAngle);
            // Adjust the size after rotation
            videoHolder.SizeToParent();
        }
    }

    public void newImageFile()
    {
        imagePath = newImageFileInputField.text;
        StartCoroutine(LoadTexture());
    }

    IEnumerator LoadTexture()
    {
        UnityWebRequest www = UnityWebRequest.Get(imagePath);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            // Retrieve results as binary data
            byte[] results = www.downloadHandler.data;

            HandleImage(results);

            if (imageHolder)
            {
                imageHolder.texture = this.texture;

                // Correct rotation based on EXIF data
                CorrectRotation();

                imageHolder.SizeToParent(); // Adjust the size
            }
        }
    }

    void HandleImage(byte[] data)
    {
        Debug.Log("Finished Getting Image -> SIZE: " + data.Length.ToString());
        ExifLib.JpegInfo jpi = ExifLib.ExifReader.ReadJpeg(data, Path.GetFileName(imagePath));

        double[] Latitude = jpi.GpsLatitude;
        double[] Longitude = jpi.GpsLongitude;
        orientationString = jpi.Orientation.ToString();

        ExifData.text = "<b>Exif Data:</b>" + "<color=white>";
        ExifData.text += "\n" + "FileName: " + jpi.FileName;
        ExifData.text += "\n" + "DateTime: " + jpi.DateTime;
        ExifData.text += "\n" + "GpsLatitude: " + Latitude[0] + "° " + Latitude[1] + "' " + Latitude[2] + '"';
        ExifData.text += "\n" + "GpsLongitude: " + Longitude[0] + "° " + Longitude[1] + "' " + Longitude[2] + '"';
        ExifData.text += "\n" + "Description: " + jpi.Description;
        ExifData.text += "\n" + "Height: " + jpi.Height + " pixels";
        ExifData.text += "\n" + "Width: " + jpi.Width + " pixels";
        ExifData.text += "\n" + "ResolutionUnit: " + jpi.ResolutionUnit;
        ExifData.text += "\n" + "UserComment: " + jpi.UserComment;
        ExifData.text += "\n" + "Make: " + jpi.Make;
        ExifData.text += "\n" + "Model: " + jpi.Model;
        ExifData.text += "\n" + "Software: " + jpi.Software;
        ExifData.text += "\n" + "Orientation: " + orientationString;
        ExifData.text += "</color>";

        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(data);
        newTexture = tex;

        this.texture = newTexture;
    }

    public void CorrectRotation()
    {
        // Rotate the image based on the EXIF orientation
        switch (orientationString)
        {
            case "TopRight": // Rotate clockwise 90 degrees
                imageRotationAngle = 90f;
                break;
            case "BottomRight": // Rotate clockwise 180 degrees
                imageRotationAngle = 180f;
                break;
            case "BottomLeft": // Rotate clockwise 270 degrees
                imageRotationAngle = 270f;
                break;
            default: // "TopLeft" and others
                imageRotationAngle = 0f;
                break;
        }

        if (usePixelRotation)
        {
            // Apply rotation in the shader
            imageRotationMaterial.SetFloat("_Rotation", imageRotationAngle);
        }
        else
        {
            // Reset RectTransform rotation
            imageHolder.rectTransform.localEulerAngles = Vector3.zero;
            // Apply rotation to the RectTransform
            imageHolder.rectTransform.localEulerAngles = new Vector3(0, 0, -imageRotationAngle);
            // Adjust the size after rotation
            imageHolder.SizeToParent();
        }
    }

    public void Rotate90Clockwise()
    {
        // Rotate the image by 90 degrees
        imageRotationAngle += 90f;
        if (imageRotationAngle >= 360f)
        {
            imageRotationAngle -= 360f;
        }

        if (usePixelRotation)
        {
            // Apply rotation in the shader
            imageRotationMaterial.SetFloat("_Rotation", imageRotationAngle);
        }
        else
        {
            // Reset RectTransform rotation
            imageHolder.rectTransform.localEulerAngles = Vector3.zero;
            // Apply rotation to the RectTransform
            imageHolder.rectTransform.localEulerAngles = new Vector3(0, 0, -imageRotationAngle);
            // Adjust the size after rotation
            imageHolder.SizeToParent();
        }
    }

    Texture2D rotateTexture(Texture2D originalTexture, bool clockwise)
    {
        Color32[] original = originalTexture.GetPixels32();
        Color32[] rotated = new Color32[original.Length];
        int w = originalTexture.width;
        int h = originalTexture.height;

        int iRotated, iOriginal;

        for (int j = 0; j < h; ++j)
        {
            for (int i = 0; i < w; ++i)
            {
                iRotated = (i + 1) * h - j - 1;
                iOriginal = clockwise ? original.Length - 1 - (j * w + i) : j * w + i;
                rotated[iRotated] = original[iOriginal];
            }
        }

        Texture2D rotatedTexture = new Texture2D(h, w);
        rotatedTexture.SetPixels32(rotated);
        rotatedTexture.Apply();
        return rotatedTexture;
    }
    
    void OnDestroy()
    {
        // Delete temporary video file
        string tempVideoPath = Path.Combine(Application.temporaryCachePath, "tempVideo.mp4");
        if (File.Exists(tempVideoPath))
        {
            File.Delete(tempVideoPath);
        }
    }
}
