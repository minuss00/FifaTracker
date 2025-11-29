import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { sessionsApi, usersApi, type SessionSummary, type User } from '../services/api';
import Modal from '../components/Modal';
import './Sessions.css';

function Sessions() {
  const navigate = useNavigate();
  const [allSessions, setAllSessions] = useState<SessionSummary[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [activeTab, setActiveTab] = useState<'active' | 'history'>('active');
  const [newSession, setNewSession] = useState({
    matchType: 'OneVsOne',
    selectedUsers: [] as string[],
  });

  const generateSessionName = () => {
    const now = new Date();
    const day = String(now.getDate()).padStart(2, '0');
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const year = now.getFullYear();
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    return `${day}-${month}-${year} ${hours}:${minutes}`;
  };
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadSessions();
    loadUsers();
  }, []);

  const loadSessions = async () => {
    try {
      setLoading(true);
      const response = await sessionsApi.getAll();
      setAllSessions(response.data);
      setError(null);
    } catch (err) {
      setError('Failed to load sessions');
      console.error(err);
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

  const handleCreateSession = async (e: React.FormEvent) => {
    e.preventDefault();
    if (newSession.selectedUsers.length < 2) {
      setError('Please select at least 2 users');
      return;
    }

    try {
      const sessionName = generateSessionName();
      await sessionsApi.create(
        sessionName,
        newSession.matchType,
        newSession.selectedUsers
      );
      setShowCreateModal(false);
      setNewSession({ matchType: 'OneVsOne', selectedUsers: [] });
      setError(null);
      loadSessions();
    } catch (err: any) {
      const errorMessage = err.response?.data?.message || err.message || 'Failed to create session';
      setError(errorMessage);
      console.error(err);
    }
  };

  const toggleUserSelection = (userId: string) => {
    setNewSession((prev) => ({
      ...prev,
      selectedUsers: prev.selectedUsers.includes(userId)
        ? prev.selectedUsers.filter((id) => id !== userId)
        : [...prev.selectedUsers, userId],
    }));
  };

  const getMatchTypeDisplay = (matchType: string) => {
    switch (matchType) {
      case 'OneVsOne': return '1v1';
      case 'TwoVsTwo': return '2v2';
      default: return matchType;
    }
  };

  const activeSessions = allSessions.filter(s => s.status === 'Active');
  const historySessions = allSessions.filter(s => s.status === 'Completed');
  const displayedSessions = activeTab === 'active' ? activeSessions : historySessions;

  return (
    <div className="sessions-page">
      <div className="page-header">
        <h2>Sessions</h2>
        <button
          onClick={() => setShowCreateModal(true)}
          className="btn btn-primary"
        >
          + New Session
        </button>
      </div>

      <Modal
        isOpen={showCreateModal}
        onClose={() => {
          setShowCreateModal(false);
          setError(null);
        }}
        title="Create New Session"
        size="medium"
      >
        <form onSubmit={handleCreateSession} className="modal-form">
          {error && <div className="error-message">{error}</div>}
          
          <p className="form-hint">Session name will be automatically set to current date and time</p>

          <div className="form-group">
            <label>Match Type</label>
            <div className="match-type-buttons">
              {['OneVsOne', 'TwoVsTwo'].map((type) => (
                <button
                  key={type}
                  type="button"
                  className={`match-type-btn ${newSession.matchType === type ? 'active' : ''}`}
                  onClick={() => setNewSession({ ...newSession, matchType: type })}
                >
                  {getMatchTypeDisplay(type)}
                </button>
              ))}
            </div>
          </div>

          <div className="form-group">
            <label>Select Players ({newSession.selectedUsers.length} selected)</label>
            <div className="user-selection-grid">
              {users.map((user) => (
                <label key={user.id} className="player-checkbox">
                  <input
                    type="checkbox"
                    checked={newSession.selectedUsers.includes(user.id)}
                    onChange={() => toggleUserSelection(user.id)}
                  />
                  <span>{user.name}</span>
                </label>
              ))}
            </div>
          </div>

          <div className="modal-actions">
            <button 
              type="submit" 
              className="btn btn-primary"
              disabled={newSession.selectedUsers.length < 2}
            >
              Create Session
            </button>
            <button
              type="button"
              onClick={() => {
                setShowCreateModal(false);
                setError(null);
              }}
              className="btn btn-secondary"
            >
              Cancel
            </button>
          </div>
        </form>
      </Modal>

      <div className="tabs">
        <button
          className={`tab ${activeTab === 'active' ? 'active' : ''}`}
          onClick={() => setActiveTab('active')}
        >
          Active ({activeSessions.length})
        </button>
        <button
          className={`tab ${activeTab === 'history' ? 'active' : ''}`}
          onClick={() => setActiveTab('history')}
        >
          History ({historySessions.length})
        </button>
      </div>

      {loading ? (
        <div className="loading">Loading sessions...</div>
      ) : (
        <div className="sessions-list">
          {displayedSessions.length === 0 ? (
            <p className="no-sessions">
              {activeTab === 'active' 
                ? 'No active sessions. Create one to get started!' 
                : 'No completed sessions yet.'}
            </p>
          ) : (
            displayedSessions.map((session) => (
              <div
                key={session.id}
                className="session-card"
                onClick={() => navigate(`/sessions/${session.id}`)}
              >
                <div className="session-card-header">
                  <h3>{session.name}</h3>
                  <span className={`status-badge ${session.status.toLowerCase()}`}>
                    {session.status}
                  </span>
                </div>
                <div className="session-card-info">
                  <div className="info-item">
                    <span className="info-label">Type:</span>
                    <span className="info-value">{getMatchTypeDisplay(session.matchType)}</span>
                  </div>
                  <div className="info-item">
                    <span className="info-label">Players:</span>
                    <span className="info-value">{session.participantCount}</span>
                  </div>
                  <div className="info-item">
                    <span className="info-label">Started:</span>
                    <span className="info-value">
                      {new Date(session.startDate).toLocaleDateString()}
                    </span>
                  </div>
                </div>
              </div>
            ))
          )}
        </div>
      )}
    </div>
  );
}

export default Sessions;
