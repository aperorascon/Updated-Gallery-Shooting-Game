using Colyseus;
 
using UnityEngine;

public class ExampleGameManager : MonoBehaviour
{
    public ColyseusNetworkedEntityView prefab;

    private void OnEnable()
    {
        ExampleRoomController.onAddNetworkEntity += OnNetworkAdd;
        ExampleRoomController.onRemoveNetworkEntity += OnNetworkRemove;
    }

    private void OnNetworkAdd(ExampleNetworkedEntity entity)
    {
        if (ExampleManager.Instance.HasEntityView(entity.id))
        {
            Debug.Log("View found! For " + entity.id);
        }
        else
        {
            Debug.Log("No View found for " + entity.id);
            CreateView(entity);
        }
    }

    private void OnNetworkRemove(ExampleNetworkedEntity entity, ColyseusNetworkedEntityView view)
    {
        RemoveView(view);
    }

    private void CreateView(ExampleNetworkedEntity entity)
    {
        Debug.Log("print: " + JsonUtility.ToJson(entity));
        ColyseusNetworkedEntityView newView = Instantiate(prefab);
        ExampleManager.Instance.RegisterNetworkedEntityView(entity, newView);
        newView.gameObject.SetActive(true);
    }

    private void RemoveView(ColyseusNetworkedEntityView view)
    {
        view.SendMessage("OnEntityRemoved", SendMessageOptions.DontRequireReceiver);
    }
}