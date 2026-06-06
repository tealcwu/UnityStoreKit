using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AppVersion : MonoBehaviour
{
    public TMP_Text VersionText;

    // Start is called before the first frame update
    void Start()
    {
        string version = Application.version;
        VersionText.text = $"v{version}";
    }
}
