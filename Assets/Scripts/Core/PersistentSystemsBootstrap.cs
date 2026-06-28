using UnityEngine;

[DisallowMultipleComponent]
public sealed class PersistentSystemsBootstrap : MonoBehaviour
{
    public static PersistentSystemsBootstrap Instance
    {
        get;
        private set;
    }

    private void Awake()
    {
        /*
         * Existiert bereits ein dauerhaftes Systemobjekt,
         * wird das neue Objekt aus der geladenen Szene
         * vollständig entfernt.
         */
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}