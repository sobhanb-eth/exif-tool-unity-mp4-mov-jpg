using UnityEngine;
using System.Collections;
using System.IO;
using UnityEngine.Networking;
using UnityEngine.UI;
using VideoLib;
using ExifLib;

public class ReadEXIF : MonoBehaviour
{
    public RawImage imageHolder; // defined in Unity Inspector
    public Text ExifData;
    // For Video
    public Text MediaData;
    public InputField newMediaFileInputField;
    public Button newMediaFileButton;

    public InputField newImageFileInputField;
    public Button newImageFileButton;
    public Button rotateButton;
    private Texture2D texture = null;
    private Texture2D newTexture;
    private string imagePath;
    private string mediaPath;
    private string orientationString;

    void Awake()
    {
        newImageFileButton.onClick.AddListener(newImageFile);
        rotateButton.onClick.AddListener(Rotate90Clockwise);

        newMediaFileButton.onClick.AddListener(LoadMediaFile);

        Debug.Log("Persistent path = " + Application.persistentDataPath);   //If you have permissions issues, put your image file here to find it!
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

        if (www.isNetworkError || www.isHttpError)
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

        if (www.isNetworkError || www.isHttpError)
        {
            Debug.Log(www.error);
        }
        else
        {
            // retrieve results as binary data
            byte[] results = www.downloadHandler.data;

            HandleImage(results);

            if (imageHolder)
            {
                imageHolder.texture = this.texture;
                CorrectRotation();
                imageHolder.SizeToParent(); // see CanvasExtensions.cs for this code
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
        // tries to use the jpi.Orientation to rotate the image properly
        newTexture = (Texture2D)imageHolder.texture;

        switch (orientationString)
        {
            case "TopRight": // Rotate clockwise 90 degrees
                newTexture = rotateTexture(newTexture, true);
                break;
            case "TopLeft": // Rotate 0 degrees...
                break;
            case "BottomRight": // Rotate clockwise 180 degrees
                newTexture = rotateTexture(newTexture, true);
                newTexture = rotateTexture(newTexture, true);
                break;
            case "BottomLeft": // Rotate clockwise 270 degrees
                newTexture = rotateTexture(newTexture, true);
                newTexture = rotateTexture(newTexture, true);
                newTexture = rotateTexture(newTexture, true);
                break;
            default:
                break;
        }

        imageHolder.texture = newTexture;
        imageHolder.SizeToParent(); // see CanvasExtensions.cs for this code
    }

    public void Rotate90Clockwise()
    {
        newTexture = (Texture2D)imageHolder.texture;
        newTexture = rotateTexture(newTexture, true); // Rotate clockwise 90 degrees
        imageHolder.texture = newTexture;
        imageHolder.SizeToParent(); // see CanvasExtensions.cs for this code
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
}
