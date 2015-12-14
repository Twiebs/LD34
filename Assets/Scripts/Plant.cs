using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;


public class Plant : MonoBehaviour {

    [System.Serializable]
    public class SceneTransition {
        public int transitionHeight;
        public Sprite backgroundSprite;
        public List<GameObject> pickupPrefabs;
        public List<GameObject> obstaclePrefabs;
    }

    private class SceneChunk {
        public GameObject sceneObject;
        public List<GameObject> activeObjects;
        public SceneChunk() {
            activeObjects = new List<GameObject>();
        }
    }

    public List<SceneTransition> sceneTransitions;
    public GameObject plantSegmentPrefab;
    public GameObject sceneObjectPrefab;
    public Camera plantCamera;

    public GameObject introUI;
    public GameObject gameUI;
    public GameObject gameOverUI;
    public GameObject victoryUI;
    public Image hydrationImage;

    private const float BASE_GROW_INCREMENT = 7.5f;
    private const float ANGLE_SPEED = 180.0f;
    private const int PLANT_SEGMENT_COUNT = 256;
    private const int SCENE_OBJECT_COUNT = 9;
    private const int SCENE_OBJECT_SIZE = 16;

    private const float CAMERA_OFFSET = 6.0f;

    private const int SPAWN_LOCATION_CACHE_SIZE = 8;

    private GameObject[] plantSegments;
    private SceneChunk[,] sceneChunks;
    private Vector3[] spawnLocationCache;
    private int currentSpawnLocationIndex = 0;

    private bool gameCanBeStarted = true;
    private bool isGrowing = false;
    private bool isEndOfGame = false;
    private bool musicIsFading = false;

    public float lastPlantLength = 0;
    public float currentPlantLength = 0;
    public float currentGrowAngle = 0;
    public int currentPlantSegmentIndex = 0;

    private int worldOriginX;
    private int worldOriginY;

    private float nextPickupHeight = 20.0f;
    private float nextObstacleHeight = 20.0f;

    private float currentHydration = 50.0f;

    private IEnumerator FadeOutUI(GameObject ui) {
        CanvasGroup canvasGroup = ui.GetComponent<CanvasGroup>();
        for (float alpha = 1.0f; alpha > 0; alpha -= 0.01f) {
            if (alpha < 0.0f) alpha = 0.0f;
            canvasGroup.alpha = alpha;
            yield return null;
        }
    }

    private IEnumerator FadeInUI(GameObject ui) {
        CanvasGroup canvasGroup = ui.GetComponent<CanvasGroup>();
        for (float alpha = 0.0f; alpha < 1.0f; alpha += 0.01f) {
            canvasGroup.alpha = alpha;
            yield return null;
        }
    }

    private IEnumerator ResetGame() {
        CanvasGroup canvasGroup = gameOverUI.GetComponent<CanvasGroup>();
        for (float alpha = 0.0f; alpha < 1.0f; alpha += 0.01f) {
            canvasGroup.alpha = alpha;
            yield return null;
        }
        ResetPlant();
        for (float alpha = 1.0f; alpha > 0; alpha -= 0.01f) {
            if (alpha < 0.0f) alpha = 0.0f;
            canvasGroup.alpha = alpha;
            yield return null;
        }
        gameCanBeStarted = true;
    }

    private IEnumerable FadeOutMusic() {
        AudioSource audioSource = GetComponent<AudioSource>();
        for (float v = 1.0f; v > 0.0f; v-=0.1f) {
            if (v < 0.0f) v = 0.0f;
            audioSource.volume = v;
            yield return null;
        }
        audioSource.Stop();
    }


    void Start() {
        spawnLocationCache = new Vector3[SPAWN_LOCATION_CACHE_SIZE];
        sceneChunks = new SceneChunk[SCENE_OBJECT_COUNT, SCENE_OBJECT_COUNT];
        plantSegments = new GameObject[PLANT_SEGMENT_COUNT];
        for (int i = 0; i < PLANT_SEGMENT_COUNT; i++)
            plantSegments[i] = GameObject.Instantiate(plantSegmentPrefab);

        worldOriginX = -((SCENE_OBJECT_COUNT - 1) / 2);
        worldOriginY = -((SCENE_OBJECT_COUNT - 1) / 2);
        for (int y = 0; y < SCENE_OBJECT_COUNT; y++) {
            for (int x = 0; x < SCENE_OBJECT_COUNT; x++) {
                float xpos = (worldOriginX * SCENE_OBJECT_SIZE) + (x * SCENE_OBJECT_SIZE);
                float ypos = (worldOriginY * SCENE_OBJECT_SIZE) + (y * SCENE_OBJECT_SIZE);
                Vector3 position = new Vector3(xpos, ypos, 0.0f);
                sceneChunks[x, y] = new SceneChunk();
                sceneChunks[x, y].sceneObject = (GameObject)GameObject.Instantiate(sceneObjectPrefab, position, Quaternion.identity);
            }
        }

        plantCamera.transform.position = new Vector3(transform.position.x,
            transform.position.y + CAMERA_OFFSET, -10);
    }


    private void ResetPlant() {
        isGrowing = false;
        currentGrowAngle = 0.0f;
        currentPlantLength = 0.0f;
        lastPlantLength = 0.0f;
        currentPlantSegmentIndex = 0;
        nextObstacleHeight = 20.0f;
        nextPickupHeight = 20.0f;
        currentHydration = 50.0f;
        musicIsFading = false;

        introUI.GetComponent<CanvasGroup>().alpha = 1.0f;
        gameUI.GetComponent<CanvasGroup>().alpha = 0.0f;

        for (int i = 0; i < PLANT_SEGMENT_COUNT; i++) {
            plantSegments[i].transform.position = Vector3.zero;
            plantSegments[i].transform.rotation = Quaternion.identity;
        }

        for (int y = 0; y < SCENE_OBJECT_COUNT; y++) {
            for (int x = 0; x < SCENE_OBJECT_COUNT; x++) {
                foreach (GameObject chunkObject in sceneChunks[x,y].activeObjects) {
                    GameObject.Destroy(chunkObject);
                }
                sceneChunks[x, y].activeObjects.Clear();
            }
        }

        worldOriginX = -((SCENE_OBJECT_COUNT - 1) / 2);
        worldOriginY = -((SCENE_OBJECT_COUNT - 1) / 2);
        for (int y = 0; y < SCENE_OBJECT_COUNT; y++) {
            for (int x = 0; x < SCENE_OBJECT_COUNT; x++) {
                float xpos = (worldOriginX * SCENE_OBJECT_SIZE) + (x * SCENE_OBJECT_SIZE);
                float ypos = (worldOriginY * SCENE_OBJECT_SIZE) + (y * SCENE_OBJECT_SIZE);
                Vector3 position = sceneChunks[x, y].sceneObject.transform.position;
                position.x = xpos;
                position.y = ypos;
                sceneChunks[x, y].sceneObject.transform.position = position;
                for (int i = 0; i < sceneTransitions.Count; i++) {
                    if ((worldOriginY + y) < sceneTransitions[i].transitionHeight) {
                        SpriteRenderer sceneSpriteRenderer = sceneChunks[x, y].sceneObject.GetComponent<SpriteRenderer>();
                        sceneSpriteRenderer.sprite = sceneTransitions[i].backgroundSprite;
                        break;
                    }
                }
            }
        }


        transform.position = Vector3.zero;
        plantCamera.transform.position = new Vector3(transform.position.x,
            transform.position.y + CAMERA_OFFSET, -10);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (other.gameObject.tag == "Pickup") {
            PickupScript pickupScript = other.GetComponent<PickupScript>();
            pickupScript.ActivatePickup();
            currentHydration += 18.0f;
        }  else {
            GameOver();
        }
    }

    public void GameOver() {
        AudioSource audioSource = GetComponent<AudioSource>();
        audioSource.Stop();
        isGrowing = false;
        StartCoroutine("ResetGame");
        if (victoryUI.GetComponent<CanvasGroup>().alpha > 0.0f) {
            StartCoroutine("FadeOutUI", victoryUI);
        }
    }



    private void MoveSceneObjectsX(int deltaX) {
        worldOriginX += deltaX;

        int indexX = deltaX == 1 ? SCENE_OBJECT_COUNT - 1 : 0;
        for (int y = 0; y < SCENE_OBJECT_COUNT; y++) {
            SceneChunk chunk = sceneChunks[indexX, y];
            foreach (GameObject gameObject in chunk.activeObjects) {
                GameObject.Destroy(gameObject);
            }
            chunk.activeObjects.Clear();
        }

        for (int y = 0; y < SCENE_OBJECT_COUNT; y++) {
            for (int x = 0; x < SCENE_OBJECT_COUNT; x++) {
                SceneChunk chunk = sceneChunks[x, y];
                Vector3 pos = chunk.sceneObject.transform.position;
                pos.x += deltaX * SCENE_OBJECT_SIZE;
                chunk.sceneObject.transform.position = pos;
            }
        }
    }

    private void MoveSceneObjectsY(int deltaY) {
        int worldDeltaY = deltaY * SCENE_OBJECT_SIZE;
        worldOriginY += deltaY;

        int indexY = deltaY == 1 ? 0 : SCENE_OBJECT_COUNT - 1;
        for (int x = 0; x < SCENE_OBJECT_COUNT; x++) {
            SceneChunk chunk = sceneChunks[x, indexY];
            foreach (GameObject gameObject in chunk.activeObjects) {
                GameObject.Destroy(gameObject);
            }

            chunk.activeObjects.Clear();
        }

        for (int y = 0; y < SCENE_OBJECT_COUNT - 1; y++) {
            for (int x = 0; x < SCENE_OBJECT_COUNT; x++) {
                SceneChunk chunk0 = sceneChunks[x, y];
                SceneChunk chunk1 = sceneChunks[x, y + 1];
                chunk0.activeObjects.AddRange(chunk1.activeObjects);
                chunk1.activeObjects.Clear();
            }
        }


        for (int y = 0; y < SCENE_OBJECT_COUNT; y++) {
            for (int x = 0; x < SCENE_OBJECT_COUNT; x++) {
                SceneChunk chunk = sceneChunks[x, y];
                Vector3 pos = chunk.sceneObject.transform.position;
                pos.y += worldDeltaY;
                chunk.sceneObject.transform.position = pos;

                for (int i = 0; i < sceneTransitions.Count; i++) {
                    if ((worldOriginY + y) < sceneTransitions[i].transitionHeight) {
                        SpriteRenderer sceneSpriteRenderer = chunk.sceneObject.GetComponent<SpriteRenderer>();
                        sceneSpriteRenderer.sprite = sceneTransitions[i].backgroundSprite;
                        break;
                    }
                }
            }
        }


    }

    private SceneTransition GetTransitionForSceneObjectY(int y) {
        for (int i = 0; i < sceneTransitions.Count; i++) {
            if (worldOriginY + y < sceneTransitions[i].transitionHeight) {
                return sceneTransitions[i];
            }
        }
        return sceneTransitions[sceneTransitions.Count - 1];
    }

    private void UpdateSceneChunks() {
        int centerIndex = ((SCENE_OBJECT_COUNT - 1) / 2);
        SceneChunk centerChunk = sceneChunks[centerIndex, centerIndex];
        Vector3 scenePosition = sceneChunks[centerIndex, centerIndex].sceneObject.transform.position;
        Vector3 segmentPosition = plantSegments[currentPlantSegmentIndex].transform.position;
        if (segmentPosition.x < scenePosition.x) MoveSceneObjectsX(-1);
        else if (segmentPosition.x > scenePosition.x + SCENE_OBJECT_SIZE) MoveSceneObjectsX(1);
        else if (segmentPosition.y < scenePosition.y) MoveSceneObjectsY(-1);
        else if (segmentPosition.y > scenePosition.y + SCENE_OBJECT_SIZE) MoveSceneObjectsY(1);
    }

    private bool InSpawnLocationValid(Vector3 location) {
        for (int i = 0; i < SPAWN_LOCATION_CACHE_SIZE; i++) {
            Vector3 displacement = location - spawnLocationCache[i];
            float magnitudeSquared = displacement.sqrMagnitude;
            if (magnitudeSquared <= 4.0f)
                return false;
        }
        return true;
    }

    private void UpdatePrefabSpawner() {
        if (transform.position.y > nextPickupHeight) {
            int chunkIndexX = -1, chunkIndexY = -1;
            Vector3 spawnPos = Vector3.zero;
            bool spawnLocationIsValid = false;
            while(!spawnLocationIsValid) {
                spawnPos = GetRandomSpawnLocation(out chunkIndexX, out chunkIndexY);
                spawnLocationIsValid = InSpawnLocationValid(spawnPos);
            }

            spawnLocationCache[currentSpawnLocationIndex] = spawnPos;
            currentSpawnLocationIndex++;
            if (currentSpawnLocationIndex >= SPAWN_LOCATION_CACHE_SIZE) {
                currentSpawnLocationIndex = 0;
            }

            SceneTransition transition = GetTransitionForSceneObjectY(chunkIndexY);
            if (transition.pickupPrefabs.Count > 0) {
                int randomIndex = Random.Range(0, transition.pickupPrefabs.Count);
                GameObject pickupObject = (GameObject)GameObject.Instantiate(transition.pickupPrefabs[randomIndex],
                    spawnPos, Quaternion.identity);
                sceneChunks[chunkIndexX, chunkIndexY].activeObjects.Add(pickupObject);
            }
            nextPickupHeight += Random.Range(8.0f, 20.0f);
        }

        if (transform.position.y > nextObstacleHeight) {
            int chunkIndexX = -1, chunkIndexY = -1;
            Vector3 spawnPos = Vector3.zero;
            bool spawnLocationIsValid = false;
            while (!spawnLocationIsValid) {
                spawnPos = GetRandomSpawnLocation(out chunkIndexX, out chunkIndexY);
                spawnLocationIsValid = InSpawnLocationValid(spawnPos);
            }

            spawnLocationCache[currentSpawnLocationIndex] = spawnPos;
            currentSpawnLocationIndex++;
            if (currentSpawnLocationIndex >= SPAWN_LOCATION_CACHE_SIZE) {
                currentSpawnLocationIndex = 0;
            }




            SceneTransition transition = GetTransitionForSceneObjectY(chunkIndexY);
            if (transition.obstaclePrefabs.Count > 0) {
                int randomIndex = Random.Range(0, transition.obstaclePrefabs.Count);
                GameObject obstacleObject = (GameObject)GameObject.Instantiate(transition.obstaclePrefabs[randomIndex],
                    spawnPos, Quaternion.identity);
                sceneChunks[chunkIndexX, chunkIndexY].activeObjects.Add(obstacleObject);
            }
            nextObstacleHeight += Random.Range(2.0f, 4.0f);
        }
    }


    private Vector3 GetRandomSpawnLocation(out int chunkIndexX, out int chunkIndexY) {
        float objectX = Mathf.Sign(Random.Range(-1.0f, 1.0f)) * Random.Range(3.0f, 13.0f) + transform.position.x;
        float objectY = Random.Range(20.0f, 32.0f) + transform.position.y;
        chunkIndexX = (int)((objectX - (float)(worldOriginX * SCENE_OBJECT_SIZE)) / SCENE_OBJECT_SIZE);
        chunkIndexY = (int)((objectY - (float)(worldOriginY * SCENE_OBJECT_SIZE)) / SCENE_OBJECT_SIZE);
        Assert.IsTrue(chunkIndexX >= 0 && chunkIndexX < SCENE_OBJECT_COUNT);
        Assert.IsTrue(chunkIndexY >= 0 && chunkIndexY < SCENE_OBJECT_COUNT);
        Vector3 objectPosition = new Vector3(objectX, objectY, 0.0f);
        return objectPosition;
    }

    private void GrowPlant() {
        float currentGrowSpeed = BASE_GROW_INCREMENT;
        currentGrowSpeed += (currentHydration / 100.0f) * 7.0f;

        currentPlantLength += currentGrowSpeed * Time.fixedDeltaTime;
        float deltaGrow = currentPlantLength - lastPlantLength;
        lastPlantLength = currentPlantLength;
        int lastSegmentIndex = currentPlantSegmentIndex;
        currentPlantSegmentIndex += 1;
        if (currentPlantSegmentIndex >= PLANT_SEGMENT_COUNT) {
            currentPlantSegmentIndex = 0;
        }

        GameObject lastSegmentObject = plantSegments[lastSegmentIndex];
        GameObject currentSegmentObject = plantSegments[currentPlantSegmentIndex];
        currentSegmentObject.transform.position = lastSegmentObject.transform.position;
        currentSegmentObject.transform.position += new Vector3(-deltaGrow * Mathf.Sin(currentGrowAngle * Mathf.Deg2Rad),
            deltaGrow * Mathf.Cos(currentGrowAngle * Mathf.Deg2Rad), 0.0f);
        currentSegmentObject.transform.rotation = Quaternion.Euler(0.0f, 0.0f, currentGrowAngle);
    }

	private void Update () {
        if (!isGrowing) {
            if ((Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D))
                && gameCanBeStarted) {
                isGrowing = true;
                gameCanBeStarted = false;
                StartCoroutine("FadeOutUI", introUI);
                StartCoroutine("FadeInUI", gameUI);
                AudioSource audioSource = GetComponent<AudioSource>();
                audioSource.Play();
            }
        } else {

            if (transform.position.y > 60.0f * SCENE_OBJECT_SIZE && musicIsFading == false) {
                StartCoroutine("FadeOutMusic");
                AudioSource audioSource = GetComponent<AudioSource>();
                audioSource.Stop();
                StartCoroutine("FadeOutUI", gameUI);
                StartCoroutine("FadeInUI", victoryUI);
                musicIsFading = true;
            }

            if (transform.position.y >= 62.0f * SCENE_OBJECT_SIZE) {
                isGrowing = false;
            }


            if (Input.GetKey(KeyCode.A)) {
                currentGrowAngle += ANGLE_SPEED * Time.deltaTime;
                currentGrowAngle = Mathf.Clamp(currentGrowAngle, -90.0f, 90.0f);
            } else if (Input.GetKey(KeyCode.D)) {
                currentGrowAngle -= ANGLE_SPEED * Time.deltaTime;
                currentGrowAngle = Mathf.Clamp(currentGrowAngle, -90.0f, 90.0f);
            }


            Vector3 targetSegmentPosition = plantSegments[currentPlantSegmentIndex].transform.position;
            transform.position = targetSegmentPosition;
            transform.rotation = Quaternion.Euler(0.0f, 0.0f, currentGrowAngle);
            Vector3 cameraTarget = new Vector3(targetSegmentPosition.x, targetSegmentPosition.y + CAMERA_OFFSET, -10.0f);
            plantCamera.transform.position = Vector3.Lerp(plantCamera.transform.position, cameraTarget, 0.1f);

            UpdateSceneChunks();
            UpdatePrefabSpawner();

            currentHydration -= Time.deltaTime * 5;
            currentHydration = Mathf.Clamp(currentHydration, 0.0f, 100.0f);
            Vector3 hydrationScale = hydrationImage.transform.localScale;
            hydrationScale.x = currentHydration / 100.0f;
            hydrationImage.transform.localScale = hydrationScale;
            
            if (currentHydration <= 0) {
                GameOver();
            }

            GrowPlant();
        }
	}
}
