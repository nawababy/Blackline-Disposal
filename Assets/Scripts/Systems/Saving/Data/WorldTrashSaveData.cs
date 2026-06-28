using System;

[Serializable]
public sealed class WorldTrashSaveData
{
    /*
     * Eindeutige ID dieses einzelnen Müllobjekts.
     *
     * Zwei verschiedene Müllsäcke dürfen niemals
     * dieselbe World Object ID besitzen.
     */
    public string worldObjectId =
        string.Empty;

    /*
     * ID der Müllart.
     *
     * Beispiele:
     * trash.garbage_bag
     * trash.body_bag
     */
    public string trashTypeId =
        string.Empty;

    /*
     * Name der Szene, in der das Objekt gespeichert wurde.
     */
    public string sceneName =
        string.Empty;

    /*
     * True:
     * Das Müllobjekt existiert noch.
     *
     * False:
     * Das Müllobjekt wurde verarbeitet oder entfernt.
     */
    public bool exists = true;

    public SerializableVector3 position =
        new SerializableVector3();

    public SerializableVector3 rotation =
        new SerializableVector3();
}