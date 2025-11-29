import { useState, useEffect } from 'react';
import { usersApi, type User, type InactiveUser } from '../services/api';
import Modal from '../components/Modal';
import ConfirmDialog from '../components/ConfirmDialog';
import './Users.css';

type Tab = 'active' | 'inactive';

function formatMinutes(minutes: number): string {
  if (minutes < 60) {
    return `${minutes}m`;
  }
  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;
  if (remainingMinutes === 0) {
    return `${hours}h`;
  }
  return `${hours}h ${remainingMinutes}m`;
}

function Users() {
  const [activeTab, setActiveTab] = useState<Tab>('active');
  const [users, setUsers] = useState<User[]>([]);
  const [inactiveUsers, setInactiveUsers] = useState<InactiveUser[]>([]);
  const [newUserName, setNewUserName] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingName, setEditingName] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(false);
  const [confirmDialog, setConfirmDialog] = useState<{
    isOpen: boolean;
    title: string;
    message: string;
    onConfirm: () => void;
  }>({ isOpen: false, title: '', message: '', onConfirm: () => {} });

  useEffect(() => {
    loadUsers();
    loadInactiveUsers();
  }, []);

  const loadUsers = async () => {
    try {
      setLoading(true);
      const response = await usersApi.getWithStats();
      setUsers(response.data);
      setError(null);
    } catch (err) {
      setError('Failed to load users');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const loadInactiveUsers = async () => {
    try {
      const response = await usersApi.getInactive();
      setInactiveUsers(response.data);
    } catch (err) {
      console.error('Failed to load inactive users:', err);
    }
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newUserName.trim()) return;

    try {
      await usersApi.create(newUserName);
      setNewUserName('');
      setShowCreateModal(false);
      loadUsers();
      setError(null);
    } catch (err: any) {
      const errorMessage = err.response?.data?.message || err.response?.data || 'Failed to create user';
      setError(errorMessage);
      console.error(err);
    }
  };

  const handleUpdate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingId || !editingName.trim()) return;

    try {
      await usersApi.update(editingId, editingName);
      setEditingId(null);
      setEditingName('');
      setShowEditModal(false);
      loadUsers();
      setError(null);
    } catch (err: any) {
      const errorMessage = err.response?.data?.message || err.response?.data || 'Failed to update user';
      setError(errorMessage);
      console.error(err);
    }
  };

  const handleDelete = (id: string, userName: string) => {
    setConfirmDialog({
      isOpen: true,
      title: 'Deactivate User',
      message: `Are you sure you want to deactivate ${userName}? User will be hidden from active lists but data will be preserved.`,
      onConfirm: async () => {
        try {
          await usersApi.delete(id);
          loadUsers();
          loadInactiveUsers();
        } catch (err) {
          setError('Failed to deactivate user');
          console.error(err);
        }
      }
    });
  };

  const handleReactivate = (id: string, userName: string) => {
    setConfirmDialog({
      isOpen: true,
      title: 'Reactivate User',
      message: `Reactivate ${userName}? User will be available for new sessions.`,
      onConfirm: async () => {
        try {
          await usersApi.reactivate(id);
          loadUsers();
          loadInactiveUsers();
        } catch (err) {
          setError('Failed to reactivate user');
          console.error(err);
        }
      }
    });
  };

  const startEdit = (user: User) => {
    setEditingId(user.id);
    setEditingName(user.name);
    setShowEditModal(true);
  };

  const cancelEdit = () => {
    setEditingId(null);
    setEditingName('');
    setShowEditModal(false);
  };

  return (
    <div className="users-page">
      <div className="page-header">
        <h2>👥 Users Management</h2>
        <button 
          onClick={() => setShowCreateModal(true)} 
          className="btn btn-primary"
        >
          ➕ Add User
        </button>
      </div>

      <div className="tabs">
        <button
          className={`tab ${activeTab === 'active' ? 'active' : ''}`}
          onClick={() => setActiveTab('active')}
        >
          Active ({users.length})
        </button>
        <button
          className={`tab ${activeTab === 'inactive' ? 'active' : ''}`}
          onClick={() => setActiveTab('inactive')}
        >
          Inactive ({inactiveUsers.length})
        </button>
      </div>

      {error && <div className="error-message">{error}</div>}

      <Modal
        isOpen={showCreateModal}
        onClose={() => {
          setShowCreateModal(false);
          setNewUserName('');
          setError(null);
        }}
        title="Add New User"
        size="small"
      >
        <form onSubmit={handleCreate} className="modal-form">
          <div className="form-group">
            <label htmlFor="user-name">User Name</label>
            <input
              id="user-name"
              type="text"
              placeholder="Enter user name"
              value={newUserName}
              onChange={(e) => setNewUserName(e.target.value)}
              className="input"
              autoFocus
            />
          </div>
          <div className="modal-actions">
            <button type="submit" className="btn btn-primary" disabled={!newUserName.trim()}>
              Create User
            </button>
            <button 
              type="button"
              onClick={() => {
                setShowCreateModal(false);
                setNewUserName('');
                setError(null);
              }} 
              className="btn btn-secondary"
            >
              Cancel
            </button>
          </div>
        </form>
      </Modal>

      <Modal
        isOpen={showEditModal}
        onClose={cancelEdit}
        title="Edit User"
        size="small"
      >
        <form onSubmit={handleUpdate} className="modal-form">
          <div className="form-group">
            <label htmlFor="edit-user-name">User Name</label>
            <input
              id="edit-user-name"
              type="text"
              value={editingName}
              onChange={(e) => setEditingName(e.target.value)}
              className="input"
              autoFocus
            />
          </div>
          <div className="modal-actions">
            <button type="submit" className="btn btn-primary" disabled={!editingName.trim()}>
              Save Changes
            </button>
            <button type="button" onClick={cancelEdit} className="btn btn-secondary">
              Cancel
            </button>
          </div>
        </form>
      </Modal>

      {loading ? (
        <div className="loading">Loading...</div>
      ) : (
        <div className="users-list">
          {activeTab === 'active' ? (
            users.length === 0 ? (
              <p className="empty-state">No active users yet. Click "Add User" to create one!</p>
            ) : (
              users.map((user) => (
                <div key={user.id} className="user-card">
                  <div className="user-info">
                    <div className="user-header">
                      <h3>{user.name}</h3>
                      {user.cardStatus !== 'None' && (
                        <span className={`card-badge card-${user.cardStatus.toLowerCase()}`}>
                          {user.cardStatus === 'Yellow' ? '🟨' : '🟥'}
                        </span>
                      )}
                    </div>
                    <div className="user-stats">
                      <div className="stat-item">
                        <span className="stat-icon">📅</span>
                        <span className="stat-label">Created:</span>
                        <span className="stat-value">{new Date(user.createdAt).toLocaleDateString()}</span>
                      </div>
                      <div className="stat-item">
                        <span className="stat-icon">🎮</span>
                        <span className="stat-label">Sessions:</span>
                        <span className="stat-value">{user.totalSessionsCount}</span>
                      </div>
                      <div className="stat-item">
                        <span className="stat-icon">⏱️</span>
                        <span className="stat-label">Total time:</span>
                        <span className="stat-value">{formatMinutes(user.totalTimeSpentMinutes)}</span>
                      </div>
                      <div className="stat-item">
                        <span className="stat-icon">👤</span>
                        <span className="stat-label">Last seen:</span>
                        <span className="stat-value">
                          {user.lastSessionDate 
                            ? new Date(user.lastSessionDate).toLocaleDateString()
                            : 'Never'}
                        </span>
                      </div>
                    </div>
                  </div>
                  <div className="button-group">
                    <button
                      onClick={() => startEdit(user)}
                      className="btn btn-secondary btn-sm"
                    >
                      ✏️ Edit
                    </button>
                    <button
                      onClick={() => handleDelete(user.id, user.name)}
                      className="btn btn-danger btn-sm"
                    >
                      🗑️ Deactivate
                    </button>
                  </div>
                </div>
              ))
            )
          ) : (
            inactiveUsers.length === 0 ? (
              <p className="empty-state">No inactive users.</p>
            ) : (
              inactiveUsers.map((user) => (
                <div key={user.id} className="user-card inactive">
                  <div className="user-info">
                    <h3>{user.name}</h3>
                    <p className="user-date">
                      Created: {new Date(user.createdAt).toLocaleDateString()}
                      <br />
                      Deactivated: {user.deactivatedAt ? new Date(user.deactivatedAt).toLocaleDateString() : 'N/A'}
                    </p>
                  </div>
                  <div className="button-group">
                    <button
                      onClick={() => handleReactivate(user.id, user.name)}
                      className="btn btn-primary btn-sm"
                    >
                      ✅ Reactivate
                    </button>
                  </div>
                </div>
              ))
            )
          )}
        </div>
      )}

      <ConfirmDialog
        isOpen={confirmDialog.isOpen}
        onClose={() => setConfirmDialog({ ...confirmDialog, isOpen: false })}
        onConfirm={confirmDialog.onConfirm}
        title={confirmDialog.title}
        message={confirmDialog.message}
        confirmText={confirmDialog.title.includes('Reactivate') ? 'Reactivate' : 'Deactivate'}
        cancelText="Cancel"
        danger={!confirmDialog.title.includes('Reactivate')}
      />
    </div>
  );
}

export default Users;
