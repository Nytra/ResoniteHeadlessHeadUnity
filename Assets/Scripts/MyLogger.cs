using TMPro;
using UnityEngine;

public class MyLogger : MonoBehaviour
{
    public TextMeshProUGUI uiText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void PushMessage(string message)
    {
        Debug.Log(message);
        if (message == null) return;
		if (uiText.text.Length > 5000)
		{
            uiText.text = uiText.text.Remove(0, 2500);
		}
		uiText.text += "\n" + message;
    }
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }
}
