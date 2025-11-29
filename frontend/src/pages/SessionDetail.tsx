import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { sessionsApi, matchesApi, usersApi, type SessionDetails, type User } from '../services/api';
import Modal from '../components/Modal';
import ConfirmDialog from '../components/ConfirmDialog';
import PlayerItem from '../components/PlayerItem';
import TeamSelector from '../components/TeamSelector';
import './SessionDetail.css';

interface SessionLeaderboardEntry {
  userId: string;
  userName: string;
  matches: number;
  wins: number;
  draws: number;
  losses: number;
  goalsScored: number;
  goalsConceded: number;
  goalDifference: number;
  points: number;
}

function SessionDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [session, setSession] = useState<SessionDetails | null>(null);
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(false);
  const [addUserError, setAddUserError] = useState<string | null>(null);
  const [customMatchError, setCustomMatchError] = useState<string | null>(null);
  const [showAddUser, setShowAddUser] = useState(false);
  const [showCustomMatch, setShowCustomMatch] = useState(false);
  const [activeTab, setActiveTab] = useState<'pending' | 'completed' | 'leaderboard' | 'players'>('completed');
  const [leaderboardMode, setLeaderboardMode] = useState<'standard' | 'effectiveness'>('standard');
  const [showGenerated, setShowGenerated] = useState(true);
  const [showCustom, setShowCustom] = useState(true);
  const [showFilters, setShowFilters] = useState(false);
  const [confirmDialog, setConfirmDialog] = useState<{
    isOpen: boolean;
    title: string;
    message: string;
    onConfirm: () => void;
  }>({ isOpen: false, title: '', message: '', onConfirm: () => {} });
  const [selectedUserId, setSelectedUserId] = useState('');
  const [customMatch, setCustomMatch] = useState({
    team1: [] as string[],
    team2: [] as string[],
  });

  useEffect(() => {
    if (id) {
      loadSession();
      loadUsers();
    }
  }, [id]);

  const loadSession = async () => {
    if (!id) return;
    try {
      setLoading(true);
      
      // First get basic session info
      const sessionResponse = await sessionsApi.getById(id);
      const isCompleted = sessionResponse.data.status === 'Completed';
      
      // Only fetch pending matches for active sessions
      const requests = [
        Promise.resolve(sessionResponse),
        isCompleted ? Promise.resolve({ data: [] }) : sessionsApi.getPendingMatches(id),
        sessionsApi.getCompletedMatches(id)
      ];
      
      const [sessionResp, pendingResponse, completedResponse] = await Promise.all(requests);
      
      const sessionData = {
        ...sessionResp.data,
        matches: pendingResponse.data, // Empty array for completed sessions
        completedMatches: completedResponse.data
      };
      setSession(sessionData);
      
      // Set initial tab based on session status
      if (sessionData.status === 'Completed' && activeTab === 'pending') {
        setActiveTab('completed');
      }
    } catch (err: any) {
      console.error('Failed to load session:', err);
    } finally {
      setLoading(false);
    }
  };

  const loadUsers = async () => {
    try {
      const response = await usersApi.getAll();
      setUsers(response.data);
    } catch (err) {
      console.error('Failed to load users:', err);
    }
  };

  const handlePauseUser = async (userId: string) => {
    if (!id) return;
    try {
      await sessionsApi.pauseUser(id, userId);
      loadSession();
    } catch (err: any) {
      console.error('Failed to pause user:', err);
    }
  };

  const handleResumeUser = async (userId: string) => {
    if (!id) return;
    try {
      await sessionsApi.resumeUser(id, userId);
      loadSession();
    } catch (err: any) {
      console.error('Failed to resume user:', err);
    }
  };

  const handleAddScore = async (
    sessionId: string,
    team1UserIds: string[],
    team2UserIds: string[],
    team1Score: number, 
    team2Score: number
  ) => {
    try {
      await matchesApi.completeMatch(sessionId, team1UserIds, team2UserIds, team1Score, team2Score);
      loadSession();
    } catch (err: any) {
      console.error('Failed to add score:', err);
    }
  };

  const handleUpdateScore = async (
    matchId: string, 
    team1Score: number, 
    team2Score: number
  ) => {
    try {
      await matchesApi.updateScore(matchId, team1Score, team2Score);
      loadSession();
    } catch (err: any) {
      console.error('Failed to update score:', err);
    }
  };

  const handleDeleteMatch = (matchId: string) => {
    setConfirmDialog({
      isOpen: true,
      title: 'Delete Match',
      message: 'Are you sure you want to delete this match?',
      onConfirm: async () => {
        try {
          await matchesApi.delete(matchId);
          loadSession();
        } catch (err: any) {
          console.error('Failed to delete match:', err);
        }
      }
    });
  };



  const handleEndSession = () => {
    if (!id) return;
    setConfirmDialog({
      isOpen: true,
      title: 'End Session',
      message: 'Are you sure you want to end this session? This action cannot be undone.',
      onConfirm: async () => {
        try {
          await sessionsApi.end(id);
          navigate('/');
        } catch (err: any) {
          console.error('Failed to end session:', err);
        }
      }
    });
  };

  const handleAddUser = async () => {
    if (!id || !selectedUserId) return;
    try {
      await sessionsApi.addUser(id, selectedUserId);
      setShowAddUser(false);
      setSelectedUserId('');
      setAddUserError(null);
      loadSession();
    } catch (err: any) {
      const errorMessage = err.response?.data?.message || err.message || 'Failed to add user';
      setAddUserError(errorMessage);
      console.error(err);
    }
  };

  const handleCreateCustomMatch = async () => {
    if (!id || customMatch.team1.length === 0 || customMatch.team2.length === 0) {
      setCustomMatchError('Both teams must have at least one player');
      return;
    }
    try {
      await matchesApi.createCustom(id, customMatch.team1, customMatch.team2);
      setShowCustomMatch(false);
      setCustomMatch({ team1: [], team2: [] });
      setCustomMatchError(null);
      loadSession();
    } catch (err: any) {
      const errorMessage = err.response?.data?.message || err.message || 'Failed to create custom match';
      setCustomMatchError(errorMessage);
      console.error(err);
    }
  };

  const togglePlayerInTeam = (userId: string, team: 'team1' | 'team2') => {
    setCustomMatch(prev => ({
      ...prev,
      [team]: prev[team].includes(userId)
        ? prev[team].filter(id => id !== userId)
        : [...prev[team], userId]
    }));
  };

  const getAvailableUsers = () => {
    if (!session) return [];
    const sessionUserIds = session.users.map((u) => u.userId);
    return users.filter((u) => !sessionUserIds.includes(u.id));
  };

  const calculateSessionLeaderboard = (): SessionLeaderboardEntry[] => {
    if (!session) return [];

    const stats = new Map<string, SessionLeaderboardEntry>();

    // Initialize stats for all session users
    session.users.forEach(user => {
      stats.set(user.userId, {
        userId: user.userId,
        userName: user.userName,
        matches: 0,
        wins: 0,
        draws: 0,
        losses: 0,
        goalsScored: 0,
        goalsConceded: 0,
        goalDifference: 0,
        points: 0,
      });
    });

    // Calculate stats from completed matches only
    const completedMatches = session.completedMatches || session.matches.filter(m => m.isCompleted);
    completedMatches.forEach(match => {
      if (!match.isCompleted || match.team1Score === undefined || match.team2Score === undefined) return;

      const team1Score = match.team1Score;
      const team2Score = match.team2Score;
      const isTeam1Win = team1Score > team2Score;
      const isDraw = team1Score === team2Score;

      // Update team 1 players
      match.team1Players.forEach(player => {
        const stat = stats.get(player.userId);
        if (stat) {
          stat.matches++;
          stat.goalsScored += team1Score;
          stat.goalsConceded += team2Score;
          if (isTeam1Win) {
            stat.wins++;
            stat.points += 3;
          } else if (isDraw) {
            stat.draws++;
            stat.points += 1;
          } else {
            stat.losses++;
          }
        }
      });

      // Update team 2 players
      match.team2Players.forEach(player => {
        const stat = stats.get(player.userId);
        if (stat) {
          stat.matches++;
          stat.goalsScored += team2Score;
          stat.goalsConceded += team1Score;
          if (!isTeam1Win && !isDraw) {
            stat.wins++;
            stat.points += 3;
          } else if (isDraw) {
            stat.draws++;
            stat.points += 1;
          } else {
            stat.losses++;
          }
        }
      });
    });

    // Calculate goal difference
    const entries = Array.from(stats.values());
    entries.forEach(entry => {
      entry.goalDifference = entry.goalsScored - entry.goalsConceded;
    });

    return entries;
  };

  const calculateEffectiveness = (entry: SessionLeaderboardEntry) => {
    const maxPoints = entry.matches * 3;
    return maxPoints > 0 ? entry.points / maxPoints : 0;
  };

  const getSessionLeaderboard = () => {
    const entries = calculateSessionLeaderboard();
    
    if (leaderboardMode === 'standard') {
      // Standard: sort by points, then goal difference, then goals scored
      return entries.sort((a, b) => {
        if (b.points !== a.points) return b.points - a.points;
        if (b.goalDifference !== a.goalDifference) return b.goalDifference - a.goalDifference;
        return b.goalsScored - a.goalsScored;
      });
    } else {
      // Effectiveness: sort by effectiveness score, then goal difference, then goals scored
      return entries.sort((a, b) => {
        const effA = calculateEffectiveness(a);
        const effB = calculateEffectiveness(b);
        if (effB !== effA) return effB - effA;
        if (b.goalDifference !== a.goalDifference) return b.goalDifference - a.goalDifference;
        return b.goalsScored - a.goalsScored;
      });
    }
  };

  if (loading) return <div className="loading">Loading...</div>;
  if (!session) return <div className="error-message">Session not found</div>;

  return (
    <div className="session-detail-page">
      <div className="page-header">
        <div>
          <h2>{session.name}</h2>
          <p className="session-info">
            Started: {new Date(session.startDate).toLocaleString()} | 
            Status: {session.status} | 
            Type: {session.matchType}
          </p>
        </div>
        <div className="button-group">
          {session.status === 'Active' && (
            <>
              <button
                onClick={() => setShowAddUser(!showAddUser)}
                className="btn btn-secondary btn-icon-mobile"
              >
                <span className="btn-icon">➕</span>
                <span className="btn-text">Add Player</span>
              </button>
              <button
                onClick={() => setShowCustomMatch(!showCustomMatch)}
                className="btn btn-secondary btn-icon-mobile"
              >
                <span className="btn-icon">⚽</span>
                <span className="btn-text">Custom Match</span>
              </button>
              <button onClick={handleEndSession} className="btn btn-danger btn-icon-mobile">
                <span className="btn-icon">🛑</span>
                <span className="btn-text">End Session</span>
              </button>
            </>
          )}
          <button onClick={() => navigate('/')} className="btn btn-secondary btn-icon-mobile">
            <span className="btn-icon">←</span>
            <span className="btn-text">Back</span>
          </button>
        </div>
      </div>

      <Modal 
        isOpen={showAddUser} 
        onClose={() => {
          setShowAddUser(false);
          setAddUserError(null);
        }}
        title="Add Player to Session"
        size="small"
      >
        <div className="modal-form">
          {addUserError && <div className="error-message">{addUserError}</div>}
          <div className="form-group">
            <label htmlFor="player-select">Select Player</label>
            <select
              id="player-select"
              value={selectedUserId}
              onChange={(e) => setSelectedUserId(e.target.value)}
              className="input"
            >
              <option value="">Choose a player...</option>
              {getAvailableUsers().map((user) => (
                <option key={user.id} value={user.id}>
                  {user.name}
                </option>
              ))}
            </select>
          </div>
          <div className="modal-actions">
            <button 
              onClick={handleAddUser} 
              className="btn btn-primary"
              disabled={!selectedUserId}
            >
              Add Player
            </button>
            <button onClick={() => setShowAddUser(false)} className="btn btn-secondary">
              Cancel
            </button>
          </div>
        </div>
      </Modal>

      <Modal
        isOpen={showCustomMatch}
        onClose={() => {
          setShowCustomMatch(false);
          setCustomMatchError(null);
        }}
        title="Create Custom Match"
        size="medium"
      >
        <div className="modal-form">
          {customMatchError && <div className="error-message">{customMatchError}</div>}
          <p className="form-hint">Select players for each team. Teams don't need to be balanced.</p>
          <TeamSelector
            users={session.users}
            team1={customMatch.team1}
            team2={customMatch.team2}
            onToggle={togglePlayerInTeam}
          />
          <div className="modal-actions">
            <button 
              onClick={handleCreateCustomMatch} 
              className="btn btn-primary"
              disabled={customMatch.team1.length === 0 || customMatch.team2.length === 0}
            >
              Create Match
            </button>
            <button onClick={() => setShowCustomMatch(false)} className="btn btn-secondary">
              Cancel
            </button>
          </div>
        </div>
      </Modal>

      <div className="tabs">
        {session.status === 'Active' && (
          <button
            className={`tab ${activeTab === 'pending' ? 'active' : ''}`}
            onClick={() => setActiveTab('pending')}
          >
            Pending Matches ({session.matches.length})
          </button>
        )}
        <button
          className={`tab ${activeTab === 'completed' ? 'active' : ''}`}
          onClick={() => setActiveTab('completed')}
        >
          Completed Matches ({(session.completedMatches || []).length})
        </button>
        <button
          className={`tab ${activeTab === 'leaderboard' ? 'active' : ''}`}
          onClick={() => setActiveTab('leaderboard')}
        >
          Leaderboard
        </button>
        <button
          className={`tab ${activeTab === 'players' ? 'active' : ''}`}
          onClick={() => setActiveTab('players')}
        >
          Players ({session.users.length})
        </button>
      </div>

      {activeTab === 'players' ? (
        <div className="session-content">
          <div className="players-section-tab full-width">
            <div className="players-list">
              {session.users.map((user) => (
                <PlayerItem
                  key={user.userId}
                  userId={user.userId}
                  userName={user.userName}
                  isActive={user.isActiveInSession}
                  totalActiveTime={user.totalActiveTime}
                  sessionStatus={session.status}
                  onPause={handlePauseUser}
                  onResume={handleResumeUser}
                />
              ))}
            </div>
          </div>
        </div>
      ) : activeTab === 'pending' ? (
        <div className="session-content">
          <div className="matches-section full-width">
            <div className="matches-controls">
              <button 
                className="filters-toggle"
                onClick={() => setShowFilters(!showFilters)}
              >
                {showFilters ? '▼' : '▶'} Filters
              </button>
              
              {showFilters && (
                <div className="matches-filters">
                  <div className="filter-section">
                    <h4>Show Type:</h4>
                    <div className="checkbox-group">
                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          checked={showGenerated}
                          onChange={(e) => setShowGenerated(e.target.checked)}
                        />
                        <span>Generated ({session.matches.filter(m => m.isGenerated && !m.isCompleted).length})</span>
                      </label>
                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          checked={showCustom}
                          onChange={(e) => setShowCustom(e.target.checked)}
                        />
                        <span>Custom ({session.matches.filter(m => !m.isGenerated && !m.isCompleted).length})</span>
                      </label>
                    </div>
                  </div>
                </div>
              )}
            </div>

            <div className="matches-list">
              {session.matches.filter(m => !m.isCompleted).length === 0 ? (
                <p className="empty-state">All matches have been played!</p>
              ) : session.matches.filter(m => !m.isCompleted && 
                  (m.isGenerated ? showGenerated : showCustom)).length === 0 ? (
                <p className="empty-state">No matches match the selected filters</p>
              ) : (
                session.matches
                  .filter(m => !m.isCompleted && (m.isGenerated ? showGenerated : showCustom))
                  .map((match) => (
                    <MatchCard
                      key={match.id}
                      match={match}
                      sessionStatus={session.status}
                      sessionId={session.id}
                      onAddScore={handleAddScore}
                      onUpdateScore={handleUpdateScore}
                      onDelete={handleDeleteMatch}
                    />
                  ))
              )}
            </div>
          </div>
        </div>
      ) : activeTab === 'completed' ? (
        <div className="session-content">
          <div className="matches-section full-width">
            <div className="matches-controls">
              <button 
                className="filters-toggle"
                onClick={() => setShowFilters(!showFilters)}
              >
                {showFilters ? '▼' : '▶'} Filters
              </button>
              
              {showFilters && (
                <div className="matches-filters">
                  <div className="filter-section">
                    <h4>Show Type:</h4>
                    <div className="checkbox-group">
                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          checked={showGenerated}
                          onChange={(e) => setShowGenerated(e.target.checked)}
                        />
                        <span>Generated ({(session.completedMatches || []).filter(m => m.isGenerated).length})</span>
                      </label>
                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          checked={showCustom}
                          onChange={(e) => setShowCustom(e.target.checked)}
                        />
                        <span>Custom ({(session.completedMatches || []).filter(m => !m.isGenerated).length})</span>
                      </label>
                    </div>
                  </div>
                </div>
              )}
            </div>

            <div className="matches-list">
              {(session.completedMatches || []).length === 0 ? (
                <p className="empty-state">No completed matches yet</p>
              ) : (session.completedMatches || [])
                  .filter(m => m.isGenerated ? showGenerated : showCustom)
                  .length === 0 ? (
                <p className="empty-state">No matches match the selected filters</p>
              ) : (
                (session.completedMatches || [])
                  .filter(m => m.isGenerated ? showGenerated : showCustom)
                  .map((match) => (
                    <MatchCard
                      key={match.id}
                      match={match}
                      sessionStatus={session.status}
                      sessionId={session.id}
                      onAddScore={handleAddScore}
                      onUpdateScore={handleUpdateScore}
                      onDelete={handleDeleteMatch}
                    />
                  ))
              )}
            </div>
          </div>
        </div>
      ) : (
        <div className="session-content">
          <div className="leaderboard-section full-width">
            <div className="leaderboard-mode-tabs">
              <button
                className={`mode-tab ${leaderboardMode === 'standard' ? 'active' : ''}`}
                onClick={() => setLeaderboardMode('standard')}
              >
                Standard Scoring
              </button>
              <button
                className={`mode-tab ${leaderboardMode === 'effectiveness' ? 'active' : ''}`}
                onClick={() => setLeaderboardMode('effectiveness')}
              >
                Effectiveness Scoring
              </button>
            </div>

            <div className="leaderboard-table-container">
              <table className="leaderboard-table">
                <thead>
                  <tr>
                    <th className="rank-col">Rank</th>
                    <th className="player-col">Player</th>
                    <th>{leaderboardMode === 'standard' ? 'Points' : 'Score'}</th>
                    {leaderboardMode === 'standard' && <th>Played</th>}
                    <th className="highlight-col">W</th>
                    <th>D</th>
                    <th className="highlight-col">L</th>
                    {leaderboardMode === 'standard' && <th>Win %</th>}
                    <th>GS</th>
                    <th>GC</th>
                    <th className="highlight-col">GD</th>
                  </tr>
                </thead>
                <tbody>
                  {getSessionLeaderboard().map((entry, index) => {
                    const effectiveness = calculateEffectiveness(entry);
                    const winRate = entry.matches > 0 ? (entry.wins / entry.matches) * 100 : 0;
                    const displayValue = leaderboardMode === 'standard' 
                      ? entry.points 
                      : effectiveness * 100;
                    const displayText = leaderboardMode === 'standard' 
                      ? displayValue.toString() 
                      : `${displayValue.toFixed(1)}%`;
                    
                    return (
                      <tr key={entry.userId} className={index < 3 ? `top-${index + 1}` : ''}>
                        <td className="rank-col">
                          {index === 0 && '🥇'}
                          {index === 1 && '🥈'}
                          {index === 2 && '🥉'}
                          {index > 2 && index + 1}
                        </td>
                        <td className="player-col">{entry.userName}</td>
                        <td className="points-col"><strong>{displayText}</strong></td>
                        {leaderboardMode === 'standard' && <td className="highlight-col black">{entry.matches}</td>}
                        <td className="highlight-col wins">{entry.wins}</td>
                        <td className="highlight-col draws">{entry.draws}</td>
                        <td className="highlight-col losses">{entry.losses}</td>
                        {leaderboardMode === 'standard' && <td className="highlight-col black">{winRate.toFixed(1)}%</td>}
                        <td className="highlight-col black">{entry.goalsScored}</td>
                        <td className="highlight-col black">{entry.goalsConceded}</td>
                        <td className={`highlight-col ${entry.goalDifference > 0 ? 'positive' : entry.goalDifference < 0 ? 'negative' : 'black'}`}>
                          {entry.goalDifference > 0 ? '+' : ''}{entry.goalDifference}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {(session.completedMatches || []).length === 0 && (
              <p className="empty-state">No completed matches yet. Play some matches to see the leaderboard!</p>
            )}
          </div>
        </div>
      )}

      <ConfirmDialog
        isOpen={confirmDialog.isOpen}
        onClose={() => setConfirmDialog({ ...confirmDialog, isOpen: false })}
        onConfirm={confirmDialog.onConfirm}
        title={confirmDialog.title}
        message={confirmDialog.message}
        confirmText="Yes"
        cancelText="No"
        danger={true}
      />
    </div>
  );
}

interface MatchCardProps {
  match: any;
  sessionStatus: 'Active' | 'Completed';
  sessionId: string;
  onAddScore: (
    sessionId: string,
    team1UserIds: string[],
    team2UserIds: string[],
    team1Score: number, 
    team2Score: number
  ) => void;
  onUpdateScore: (
    matchId: string, 
    team1Score: number, 
    team2Score: number
  ) => void;
  onDelete: (matchId: string) => void;
}

function MatchCard({ match, sessionStatus, sessionId, onAddScore, onUpdateScore, onDelete }: MatchCardProps) {
  const [showScoreModal, setShowScoreModal] = useState(false);
  const [team1Score, setTeam1Score] = useState(match.team1Score ?? 0);
  const [team2Score, setTeam2Score] = useState(match.team2Score ?? 0);
  
  const isSessionActive = sessionStatus === 'Active';

  const openScoreModal = () => {
    setTeam1Score(match.team1Score ?? 0);
    setTeam2Score(match.team2Score ?? 0);
    setShowScoreModal(true);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const team1UserIds = match.team1Players.map((p: any) => p.userId);
    const team2UserIds = match.team2Players.map((p: any) => p.userId);
    
    if (match.isCompleted) {
      // Editing existing match - use update endpoint
      onUpdateScore(match.id, team1Score, team2Score);
    } else {
      // Adding score to new match - use complete endpoint
      onAddScore(sessionId, team1UserIds, team2UserIds, team1Score, team2Score);
    }
    
    setShowScoreModal(false);
  };

  const adjustScore = (team: 'team1' | 'team2', delta: number) => {
    if (team === 'team1') {
      setTeam1Score((prev: number) => Math.max(0, prev + delta));
    } else {
      setTeam2Score((prev: number) => Math.max(0, prev + delta));
    }
  };

  const team1Names = match.team1Players.map((p: any) => p.userName).join(' & ');
  const team2Names = match.team2Players.map((p: any) => p.userName).join(' & ');

  return (
    <div className={`match-card ${match.isCompleted ? 'completed' : 'pending'}`}>
      <div className="match-header">
        <span className="match-badge">
          {match.isGenerated ? '🤖 Auto' : '✏️ Custom'}
        </span>
        {match.isCompleted && match.playedAt && (
          <span className="match-date">
            {new Date(match.playedAt).toLocaleTimeString()}
          </span>
        )}
      </div>

      <div className="match-content">
        <div className="team team-1">
          <div className="team-players">{team1Names}</div>
        </div>

        <div className="match-score-section">
          <div className="score-display">
            {match.isCompleted ? (
              <div className="score-value">
                {match.team1Score} : {match.team2Score}
              </div>
            ) : (
              <div className="score-value pending-text">vs</div>
            )}
          </div>
        </div>

        <div className="team team-2">
          <div className="team-players">{team2Names}</div>
        </div>
      </div>

      {isSessionActive && (
        <div className="match-actions">
          {match.isCompleted ? (
            <button
              onClick={openScoreModal}
              className="btn btn-secondary btn-sm"
            >
              ✏️ Edit Score
            </button>
          ) : (
            <button
              onClick={openScoreModal}
              className="btn btn-primary btn-sm"
            >
              ➕ Add Score
            </button>
          )}
          <button
            onClick={() => onDelete(match.id)}
            className="btn btn-danger btn-sm"
            title="Delete match"
          >
            🗑️ Delete
          </button>
        </div>
      )}

      <Modal
        isOpen={showScoreModal}
        onClose={() => setShowScoreModal(false)}
        title={match.isCompleted ? "Edit Match Score" : "Add Match Score"}
        size="medium"
      >
        <form onSubmit={handleSubmit} className="score-modal-form">
          <div className="score-modal-teams">
            <div className="score-modal-team">
              <div className="team-name-header">{team1Names}</div>
              <div className="score-controls">
                <button
                  type="button"
                  onClick={() => adjustScore('team1', -1)}
                  className="btn-score-adjust"
                  disabled={team1Score === 0}
                >
                  −
                </button>
                <div className="score-display-large">{team1Score}</div>
                <button
                  type="button"
                  onClick={() => adjustScore('team1', 1)}
                  className="btn-score-adjust"
                >
                  +
                </button>
              </div>
            </div>

            <div className="score-separator-large">:</div>

            <div className="score-modal-team">
              <div className="team-name-header">{team2Names}</div>
              <div className="score-controls">
                <button
                  type="button"
                  onClick={() => adjustScore('team2', -1)}
                  className="btn-score-adjust"
                  disabled={team2Score === 0}
                >
                  −
                </button>
                <div className="score-display-large">{team2Score}</div>
                <button
                  type="button"
                  onClick={() => adjustScore('team2', 1)}
                  className="btn-score-adjust"
                >
                  +
                </button>
              </div>
            </div>
          </div>

          <div className="modal-actions">
            <button type="submit" className="btn btn-primary">
              💾 Save Score
            </button>
            <button
              type="button"
              onClick={() => setShowScoreModal(false)}
              className="btn btn-secondary"
            >
              Cancel
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}

export default SessionDetail;
