using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class RollicCaseSDK : Singleton<RollicCaseSDK>
{
    private float sessionStartTime;

    private Queue<TrackedEvent> savedEventQueue = new Queue<TrackedEvent>();
    private string apiUrl = "https://exampleapi.rollic.gs/event";

    private const string QueueFileName = "eventQueue.json";

    private float reSendTimeUnsuccesfullSavedEvents = 5f;
    private Coroutine resendUnsuccessfullDataCoroutine;

    public void Initialize()
    {
        Debug.Log("Application.persistentDataPath" + Application.persistentDataPath.ToString());
        sessionStartTime = Time.realtimeSinceStartup;
        LoadQueueFromDisk();
        TryStartResendCoroutine();
    }

    public void TrackEvent(string eventName)
    {
        float sessionTime = Time.realtimeSinceStartup - sessionStartTime;

        TrackedEvent tracked = new TrackedEvent
        {
            @event = eventName,
            session_time = sessionTime
        };

        StartCoroutine(SendRequest(tracked));
    }

    private IEnumerator SendRequest(TrackedEvent track, bool isAlreadySavedData = false)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("Failed to send event: There is not internet connection");
            UnsuccessfullSendRequestProcess(isAlreadySavedData, track);
            yield break;
        }

        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST", new DownloadHandlerBuffer(),
            new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(track))));

        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300)
        {
            Debug.Log("Data Sent Successfully: " + request.responseCode);
            if (isAlreadySavedData)
            {
                savedEventQueue.Dequeue();
                SaveQueueToDisk();
            }
        }
        else
        {
            Debug.LogWarning("Failed to send event: " + request.error);
            UnsuccessfullSendRequestProcess(isAlreadySavedData, track);
        }
    }

    private void UnsuccessfullSendRequestProcess(bool isAlreadySavedData, TrackedEvent track)
    {
        if (isAlreadySavedData)
            return;
        savedEventQueue.Enqueue(track);
        SaveQueueToDisk();
        TryStartResendCoroutine();
    }

    private void TryStartResendCoroutine()
    {
        if (resendUnsuccessfullDataCoroutine == null && savedEventQueue.Count > 0)
        {
            resendUnsuccessfullDataCoroutine = StartCoroutine(SendUnsuccesfullTrackingData());
        }
    }

    private IEnumerator SendUnsuccesfullTrackingData()
    {
        while (savedEventQueue.Count > 0)
        {
            if (Application.internetReachability != NetworkReachability.NotReachable)
                yield return StartCoroutine(SendRequest(savedEventQueue.Peek(), true));
            else
                yield return new WaitForSeconds(reSendTimeUnsuccesfullSavedEvents);
        }

        resendUnsuccessfullDataCoroutine = null;
    }

    private void SaveQueueToDisk()
    {
        string path = Path.Combine(Application.persistentDataPath, QueueFileName);

        string json = JsonUtility.ToJson(new TrackedEventList { events = new List<TrackedEvent>(savedEventQueue) });
        Debug.Log(" saved json:   " + json);

        try
        {
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error saving event queue: " + ex.Message);
        }
    }

    private void LoadQueueFromDisk()
    {
        string path = Path.Combine(Application.persistentDataPath, QueueFileName);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            TrackedEventList saved = JsonUtility.FromJson<TrackedEventList>(json);

            if (saved != null && saved.events != null)
            {
                foreach (var e in saved.events)
                {
                    savedEventQueue.Enqueue(e);
                }
            }
        }
    }

    void OnApplicationQuit()
    {
        savedEventQueue.Enqueue(new TrackedEvent
        {
            @event = "session end",
            session_time = Time.realtimeSinceStartup - sessionStartTime
        });
        SaveQueueToDisk();
    }

    [Serializable]
    private class TrackedEvent
    {
        public string @event;
        public float session_time;
    }

    [Serializable]
    private class TrackedEventList
    {
        public List<TrackedEvent> events;
    }
}