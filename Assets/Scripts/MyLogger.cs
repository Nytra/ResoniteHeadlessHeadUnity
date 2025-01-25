using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class MyLogger : MonoBehaviour
{
    public TextMeshProUGUI uiText;
    public static MyLogger instance;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void PushMessage(string message)
    {
        
        Debug.Log(message);
        if (message == null) return;
		if (uiText.text.Length > 10000)
		{
            uiText.text = uiText.text.Remove(0, 2500);
		}
		uiText.text += "\n" + message;
    }
    void Start()
    {
		instance = this;
	}

    // Update is called once per frame
    void Update()
    {
    }
}
