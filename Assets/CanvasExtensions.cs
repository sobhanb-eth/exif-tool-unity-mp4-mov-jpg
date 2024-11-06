using UnityEngine;
using UnityEngine.UI;

public static class CanvasExtensions
{
    public static void SizeToParent(this RawImage image, float padding = 0.0f)
    {
        RectTransform parent = image.transform.parent as RectTransform;
        RectTransform imageTransform = image.rectTransform;

        if (parent == null)
            return;

        // Set pivot to center
        imageTransform.pivot = new Vector2(0.5f, 0.5f);

        // Calculate the available size in the parent, considering padding
        float paddingHorizontal = padding * 2;
        float paddingVertical = padding * 2;

        float parentWidth = parent.rect.width - paddingHorizontal;
        float parentHeight = parent.rect.height - paddingVertical;

        // Get the rotation angle in radians
        float angle = imageTransform.eulerAngles.z * Mathf.Deg2Rad;

        // Calculate the cosine and sine of the angle
        float cos = Mathf.Abs(Mathf.Cos(angle));
        float sin = Mathf.Abs(Mathf.Sin(angle));

        // Calculate the dimensions of the rotated image
        float imageWidth = image.texture.width;
        float imageHeight = image.texture.height;
        float imageRatio = imageWidth / imageHeight;

        // Effective width and height after rotation
        float effectiveWidth = imageWidth * cos + imageHeight * sin;
        float effectiveHeight = imageWidth * sin + imageHeight * cos;

        // Scaling factors to fit the rotated image within the parent
        float scaleX = parentWidth / effectiveWidth;
        float scaleY = parentHeight / effectiveHeight;

        // Use the smaller scale to maintain aspect ratio
        float scale = Mathf.Min(scaleX, scaleY);

        // Apply the scale to the original image dimensions
        float finalWidth = imageWidth * scale;
        float finalHeight = imageHeight * scale;

        // Set the size of the RectTransform
        imageTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalWidth);
        imageTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalHeight);

        // Center the image
        imageTransform.anchoredPosition = Vector2.zero;
    }
}