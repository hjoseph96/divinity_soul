using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Grids2D;
using Com.LuisPedroFonseca.ProCamera2D;

public class GridBattleManager : GridInterface
{
    public List<MapEntity> allEntities;

    public CellHighlightsDictionary cellHighlights;
    public GameObject mapCursor;

   
    public ProCamera2D gridCamera;

    private Cell _selectedCell;
    public Cell SelectedCell {
        get { return _selectedCell; }
        set {
            _selectedCell = value;
        }
    }
  

    private string _currentPhase;
    private int _turnNumber;
    private Dictionary<(int, string), CellHighlighter> _activeHighLights = new Dictionary<(int, string), CellHighlighter>();
    private GridSelect _selector;
    private List<string> _SELECTION_STATES = new List<string>{ "FREE", "MOVING" };
    private string _selectionState;
    private PlayerEntity _currentlyMovingPlayer;
    private int _movingFromCellIndex;
    private List<int> _landObstacleCells = new List<int>();
    private List<int> _airObstacleCells = new List<int>();

    // Start is called before the first frame update
    new void Start()
    {
        base.Start();

        _turnNumber = 0;
        _currentPhase = PHASES[0];

        mapCursor = Instantiate(mapCursor) as GameObject;
        _selector = mapCursor.GetComponent<GridSelect>();

        mapCursor.SetActive(false);

        
        OnFirstScan.AddListener(delegate{
            // maybe scan them every frame?
            SetupEntityLists();
            SetupCellLists();
            ScanCells();

            _selector.SetCursor(this, _cellPositions);


            // Set highlighted cell to first player
            if (_currentPhase == "PLAYER") {
                _selectionState = "FREE";
                SetSelectedCellOnPlayer();
            }

            gridCamera.RemoveAllCameraTargets();
            gridCamera.AddCameraTarget(mapCursor.transform);
            PositionEntities();
        });
    }

    // Update is called once per frame
    new void Update()
    {
        base.Update();

        HighlightSelectedCell();

        ProcessPhase();
    }

    // TESTING CELL BOUNDS
    // void OnDrawGizmos()
    // {
    //     Gizmos.color = Color.red;
    //     foreach (KeyValuePair<int, Bounds> entry in _cellPositions)
    //     {
    //         Gizmos.DrawWireCube(_grid.CellGetPosition(entry.Key), new Vector3(0.32f, 0.32f, 100));
    //     }
    // }
    
    void HighlightSelectedCell() {
        bool alreadySet = false;
        
        List<CellHighlighter> selectedHighlights = GetHighlightsByType("SELECTED");
        
        if (selectedHighlights.Count > 0) {
            foreach(CellHighlighter selectedHighlight in selectedHighlights) {
                Cell highlightedCell = GetCellAtPosition(selectedHighlight.transform.position);

                if (highlightedCell.index != _selectedCell.index) {
                    RemoveHighlightFromCell(highlightedCell.index);
                } else {
                    alreadySet = true;
                }
            }
        }

        if (!alreadySet)
            SetCellHighlight("SELECTED", _selectedCell.index);
    }




    void NextTurn() {
        _turnNumber += 1;
        _currentPhase =  PHASES[0];

        // TODO: GUI PHASE DISPLAY
    }

    void NextPhase() {
        int phaseIndex = PHASES.IndexOf(_currentPhase);

        if (phaseIndex == PHASES.Count - 1) _currentPhase = PHASES[0];

        _currentPhase = PHASES[phaseIndex + 1];
    }

    void ProcessPhase() {
        switch (_currentPhase)
        {
            case "PLAYER":
                ProcessPlayerPhase();
                break;
            case "ENEMY":
                break;
            case "OTHER_ENEMY":
                break;
            case "ALLY":
                break;
            default:
                break;
        }
    }

    void ProcessPlayerPhase() {
        switch (_selectionState) {
            case "FREE":
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z)) {
                    foreach(PlayerEntity player in _players) {
                        bool isPlayerSelected = _selectedCell.index == _playerCells[player].index;

                        if (isPlayerSelected && player.hasMoved == false) {
                            _selectionState = "MOVING";
                            _currentlyMovingPlayer = player;
                            _movingFromCellIndex = _selectedCell.index;
                        }
                    }   
                }
                break;
            case "MOVING":
                int moveRadius = _currentlyMovingPlayer.BASE_STATS["MOVE"].CalculateValue();

                List<int> movableCellIndices = MoveableCells(_movingFromCellIndex, moveRadius);
                _selector.SetCellLimits(movableCellIndices);

                foreach(int cellIndex in movableCellIndices) {
                    if (IsEnemyWithinCell(cellIndex)) {
                        SetCellHighlight("ATTACK", cellIndex);
                    } else if (_grid.cells[cellIndex].canCross) {
                        SetCellHighlight("MOVE", cellIndex);
                    }
                }

                break;
        }
    }

    List<int> MoveableCells(int cellIndex, int moveRadius) {
        List<int> moveRange = _grid.CellGetNeighbours(cellIndex, moveRadius);

        if (!_currentlyMovingPlayer.CanFly) moveRange = moveRange.Except(_landObstacleCells).ToList();
        // if (_currentlyMovingPlayer.CanFly) moveRange = (List<int>)moveRange.Except(_airObstacleCells); TODO

        moveRange.Add(cellIndex);
        return moveRange;
    }

    bool IsEnemyWithinCell(int cellIndex) {
        bool isEnemyInCell = false;

        if (_enemies.Count > 0) {
            foreach(EnemyEntity enemy in _enemies) {
                int enemyCellIndex = _enemyCells[enemy].index;
                Vector3 enemyPosition = _cellPositions[enemyCellIndex].center;;
                
                if (WithinCell(_cellPositions[cellIndex], enemyPosition)) {
                    isEnemyInCell = true;
                    break;
                }
            }
        }

        if (_otherEnemies.Count > 0) {
            foreach(OtherEnemyEntity otherEnemy in _otherEnemies) {
                Bounds otherEnemyPosition = _cellPositions[_otherEnemyCells[otherEnemy].index];
                
                if (WithinCellAsBounds(_cellPositions[cellIndex], otherEnemyPosition)) {
                    isEnemyInCell = true;
                    break;
                }
            }
        }

        return isEnemyInCell;
    }

    void SetCellHighlight(string type, int cellIndex) {
        if (!cellHighlights.Keys.Contains(type)) throw new System.Exception($"Cell Highlight type: '{type}' is invalid.");
        if (_activeHighLights.ContainsKey((cellIndex, type))) return;

        Vector3 cellPosition = _cellPositions[cellIndex].center;
        CellHighlighter highlightPrefab = cellHighlights[type];

        GameObject highlightObj = Instantiate(highlightPrefab.gameObject, cellPosition, highlightPrefab.transform.rotation);
        CellHighlighter highlight = highlightObj.GetComponent<CellHighlighter>();

        _activeHighLights.Add((cellIndex, type), highlight);
    }

    void RemoveAllHighlights(bool untracked = false) {
        foreach(KeyValuePair<(int, string), CellHighlighter> entry in _activeHighLights) {
            Destroy(entry.Value.gameObject);

            _activeHighLights.Remove(entry.Key);
        }

        if (untracked) {
            CellHighlighter[] highlights = GetComponents<CellHighlighter>();

            if (highlights.Length > 0) {
                foreach(CellHighlighter highlight in highlights) Destroy(highlight.gameObject);
            }
        }
    }

    (int, string) isCellHighlighted(int cellIndex) {
        (int, string) cellIndexAndHighlightType = default;

        foreach(KeyValuePair<(int, string), CellHighlighter> entry in _activeHighLights)
            if (entry.Key.Item1 == cellIndex) cellIndexAndHighlightType = entry.Key;

        return cellIndexAndHighlightType;
    }

    void RemoveHighlightFromCell(int cellIndex) {
        (int, string) highlightInfo = isCellHighlighted(cellIndex); 
        if (highlightInfo.Equals(default))
            throw new System.Exception($"Cell Index: {cellIndex} is not highlighted or is untracked by the manager.");

        Destroy(_activeHighLights[highlightInfo].gameObject);

        _activeHighLights.Remove(highlightInfo);
    }

    bool HasHighlightType(string type) {
        if (!cellHighlights.Keys.Contains(type)) throw new System.Exception($"Cell Highlight type: '{type}' is invalid.");

        bool hasType = false;

        foreach(KeyValuePair<(int, string), CellHighlighter> entry in _activeHighLights)
            if (entry.Key.Item2 == type) hasType = true;

        return hasType;
    }

    List<CellHighlighter> GetHighlightsByType(string type) {
        if (!cellHighlights.Keys.Contains(type)) throw new System.Exception($"Cell Highlight type: '{type}' is invalid.");

        List<CellHighlighter> highlightsByType = new List<CellHighlighter>();

        if (!HasHighlightType(type)) return highlightsByType;

        foreach(KeyValuePair<(int, string), CellHighlighter> entry in _activeHighLights) {
            if (entry.Key.Item2 == type)
                highlightsByType.Add(entry.Value);
        }

        return highlightsByType;
    }

    void SetSelectedCellOnPlayer() {
        foreach(PlayerEntity player in _players) {
            if (player.hasMoved)
                continue;
            
            Cell currentCell = _playerCells[player];
            SelectedCell = currentCell;

            Vector3 cursorPosition = _grid.CellGetPosition(_selectedCell.index);
            if (!mapCursor.activeSelf)
                mapCursor.SetActive(true);
                
            mapCursor.transform.position = cursorPosition;

            break;
        } 
    }

    void SetupEntityLists() {
        foreach(MapEntity entity in allEntities) {
            if (entity.EntityType == "player") AddPlayer(entity);
            if (entity.EntityType == "ally")  AddAlly(entity);
            if (entity.EntityType == "enemy") AddEnemy(entity);
            if (entity.EntityType == "other_enemy") AddOtherEnemy(entity);
        }
    }

    void SetupCellLists() {
        _playerCells    = PlayerInhabitingCells();
        _allyCells      = AllyInhabitingCells();
        _enemyCells     = EnemyInhabitingCells();
        _otherEnemyCells = OtherEnemyInhabitingCells();
    
    }


    void PositionEntities() {
        foreach (KeyValuePair<PlayerEntity, Cell> entry in _playerCells) 
            entry.Key.transform.position = _cellPositions[entry.Value.index].center;

        foreach (KeyValuePair<EnemyEntity, Cell> entry in _enemyCells) 
            entry.Key.transform.position = _cellPositions[entry.Value.index].center;

        foreach (KeyValuePair<OtherEnemyEntity, Cell> entry in _otherEnemyCells) 
            entry.Key.transform.position = _cellPositions[entry.Value.index].center;

        foreach (KeyValuePair<AllyEntity, Cell> entry in _allyCells) 
            entry.Key.transform.position = _cellPositions[entry.Value.index].center; 
    }

    void SetLandObstables(GameObject obstacle, int cellIndex, Bounds cellBounds) {
        List<Bounds> objBounds = new List<Bounds>();
        Collider[] colliders = obstacle.GetComponents<Collider>();
        
        foreach(Collider col in colliders)
            objBounds.Add(col.bounds);

        if (WithinCellAsColliders(cellBounds, objBounds)) {
            // _grids.cells[cellIndex].canCross = selectedPlayer.canFly;

            _landObstacleCells.Add(cellIndex);
            
            _grid.CellSetGroup (cellIndex, CELL_MASKS["IMMOVABLE_ON_LAND"]);
            _grid.CellSetTag(cellIndex, CELL_MASKS["IMMOVABLE_ON_LAND"]);
            
            // Assign a crossing cost to barrier for path-finding purposes
            // _grid.CellSetSideCrossCost (cellIndex, CELL_SIDE.Top, barrierCost);
            // _grid.CellSetSideCrossCost (cellIndex, CELL_SIDE.Left, barrierCost);
            // _grid.CellSetSideCrossCost (cellIndex, CELL_SIDE.Right, barrierCost);
            // _grid.CellSetSideCrossCost (cellIndex, CELL_SIDE.Bottom, barrierCost);
        }
    }
   
    void ScanCells()
    {
        GameObject[] landObstacles = GameObject.FindGameObjectsWithTag("Land Obstacle");

        for(int i = 0; i < _cellPositions.Count; i++) {
            int cellIndex = i;
            Bounds cellBounds = _cellPositions[cellIndex];

            foreach (GameObject obstacle in landObstacles)
                SetLandObstables(obstacle, cellIndex, cellBounds);
        }
    }
}
