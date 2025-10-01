using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Diagnostics;

public class BubbleSort : MonoBehaviour
{
    float[] array;
    List<GameObject> mainObjects;
    public GameObject prefab;
    Stopwatch stopwatch;

    void Start()
    {
        mainObjects = new List<GameObject>();
        array = new float[30000];
        for (int i = 0; i < 30000; i++)
        {
            array[i] = (float)Random.Range(0, 1000)/100;
        }

        stopwatch = new Stopwatch();

        //TO DO 4
        //Call the three previous functions in order to set up the exercise
        logArray(); // Print initial array state
        spawnObjs(); // Create visual representation

        // SINGLE-THREADED APPROACH (blocks main thread, game freezes)
        // Uncomment the line below to test single-threaded approach
        // bubbleSort();

        //TO DO 5
        //Create a new thread using the function "bubbleSort" and start it.
        // MULTI-THREADED APPROACH (non-blocking, game continues)

        // Choose which sorting algorithm to use:
        // Option 1: BubbleSort (O(n²) - slower)
        Thread sortThread = new Thread(bubbleSort);

        // Option 2: QuickSort (O(n log n) - faster)
        // Uncomment below and comment BubbleSort to compare
        // Thread sortThread = new Thread(quickSortWrapper);

        sortThread.Start();

    }

    void Update()
    {
        //TO DO 6
        //Call ChangeHeights() in order to update our object list.
        //Since we'll be calling UnityEngine functions to retrieve and change some data,
        //we can't call this function inside a Thread
        updateHeights();

    }

    //TO DO 5
    //Create a new thread using the function "bubbleSort" and start it.
    void bubbleSort()
    {
        stopwatch.Start();
        UnityEngine.Debug.Log("BubbleSort started...");

        int i, j;
        int n = array.Length;
        bool swapped;
        for (i = 0; i < n- 1; i++)
        {
            swapped = false;
            for (j = 0; j < n - i - 1; j++)
            {
                if (array[j] > array[j + 1])
                {
                    (array[j], array[j+1]) = (array[j+1], array[j]);
                    swapped = true;
                }
            }
            if (swapped == false)
                break;
        }

        stopwatch.Stop();
        UnityEngine.Debug.Log($"BubbleSort completed in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F2}s)");
        //You may debug log your Array here in case you want to. It will only be called one the bubble algorithm has finished sorting the array
    }

    // DELIVERABLE 1: QuickSort Algorithm
    // QuickSort is much faster than BubbleSort: O(n log n) vs O(n²)
    void quickSortWrapper()
    {
        stopwatch.Start();
        UnityEngine.Debug.Log("QuickSort started...");

        quickSort(0, array.Length - 1);

        stopwatch.Stop();
        UnityEngine.Debug.Log($"QuickSort completed in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F2}s)");
    }

    void quickSort(int low, int high)
    {
        if (low < high)
        {
            int pi = partition(low, high);
            quickSort(low, pi - 1);
            quickSort(pi + 1, high);
        }
    }

    int partition(int low, int high)
    {
        float pivot = array[high];
        int i = (low - 1);

        for (int j = low; j < high; j++)
        {
            if (array[j] < pivot)
            {
                i++;
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
        (array[i + 1], array[high]) = (array[high], array[i + 1]);
        return i + 1;
    }

    void logArray()
    {
        string text = "";

        //TO DO 1
        //Simply show in the console what's inside our array.
        for (int i = 0; i < array.Length; i++)
        {
            text += array[i].ToString("F2") + " ";
        }

        UnityEngine.Debug.Log(text);
    }
    
    void spawnObjs()
    {
        //TO DO 2
        //We should be storing our objects in a list so we can access them later on.

        for (int i = 0; i < array.Length; i++)
        {
            //We have to separate the objs accordingly to their width, in which case we divide their position by 1000.
            //If you decide to make your objs wider, don't forget to up this value

            GameObject obj = Instantiate(prefab, new Vector3((float)i / 1000,
                this.gameObject.GetComponent<Transform>().position.y, 0), Quaternion.identity);
            mainObjects.Add(obj);
        }

    }

    //TO DO 3
    //We'll just change the height of every obj in our list to match the values of the array.
    //To avoid calling this function once everything is sorted, keep track of new changes to the list.
    //If there weren't, you might as well stop calling this function

    bool updateHeights()
    {

        bool changed = false;
        for (int i = 0; i < array.Length; i++)
        {
            Vector3 currentScale = mainObjects[i].transform.localScale;
            if (currentScale.y != array[i])
            {
                mainObjects[i].transform.localScale = new Vector3(currentScale.x, array[i], currentScale.z);
                changed = true;
            }
        }
        return changed;
    }
}
