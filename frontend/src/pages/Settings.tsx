import { useEffect, useState } from 'react';
import { backupApi, type BackupInfo } from '../services/api';
import ConfirmDialog from '../components/ConfirmDialog';
import { showToast } from '../components/ToastContainer';
import './Settings.css';

function Settings() {
  const [backups, setBackups] = useState<BackupInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>('');
  const [selectedBackup, setSelectedBackup] = useState<string>('');
  const [showRestoreDialog, setShowRestoreDialog] = useState(false);

  useEffect(() => {
    loadBackups();
  }, []);

  const loadBackups = async () => {
    try {
      setLoading(true);
      const response = await backupApi.list();
      setBackups(response.data.backups);
      setError('');
    } catch (err) {
      setError('Failed to load backups');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateBackup = async () => {
    try {
      setLoading(true);
      const response = await backupApi.create();
      showToast('Backup created successfully!', 'success');
      await loadBackups(); // Refresh the list
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleRestoreClick = (fileName: string) => {
    setSelectedBackup(fileName);
    setShowRestoreDialog(true);
  };

  const handleRestoreConfirm = async () => {
    try {
      setLoading(true);
      setShowRestoreDialog(false);
      const response = await backupApi.restore(selectedBackup);
      showToast('Database restored successfully!', 'success');

      // Refresh backups list
      await loadBackups();

      // Clear selection
      setSelectedBackup('');
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString();
  };

  const formatFileSize = (bytes: number) => {
    const mb = bytes / (1024 * 1024);
    return mb.toFixed(2) + ' MB';
  };

  if (loading && backups.length === 0) {
    return <div className="settings-page">Loading...</div>;
  }

  return (
    <div className="settings-page">
      <h1 className="black">⚙️ Settings</h1>

      {error && <div className="error-message">{error}</div>}

      {/* Backup Section */}
      <div className="settings-section">
        <h2>Database Backup</h2>
        <p>Create a full backup of your database. Backups are stored locally and include all your matches, sessions, and player data.</p>

        <button
          className="action-button primary"
          onClick={handleCreateBackup}
          disabled={loading}
        >
          {loading ? 'Creating Backup...' : '📦 Create Backup'}
        </button>
      </div>

      {/* Restore Section */}
      <div className="settings-section">
        <h2>Database Restore</h2>
        <p>Restore your database from a previous backup. This will completely replace your current data.</p>
        <div className="warning-message">
          <strong>⚠️ Warning:</strong> Restoring will permanently replace all current data. A safety backup is automatically created before restoration.
        </div>

        {backups.length === 0 ? (
          <p className="no-data">No backups available. Create your first backup above!</p>
        ) : (
          <div className="backup-list">
            <h3>Available Backups</h3>
            <div className="backup-items">
              {backups.map((backup) => (
                <div key={backup.fileName} className="backup-item">
                  <div className="backup-info">
                    <div className="backup-name">{backup.fileName}</div>
                    <div className="backup-details">
                      Created: {formatDate(backup.createdDate)} • Size: {formatFileSize(backup.size)}
                    </div>
                  </div>
                  <button
                    className="action-button danger small"
                    onClick={() => handleRestoreClick(backup.fileName)}
                    disabled={loading}
                  >
                    Restore
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      <ConfirmDialog
        isOpen={showRestoreDialog}
        onClose={() => setShowRestoreDialog(false)}
        onConfirm={handleRestoreConfirm}
        title="Confirm Database Restore"
        message={`Are you sure you want to restore the database from "${selectedBackup}"? This will completely replace all current data. A safety backup will be created automatically before restoration.`}
        confirmText="Yes, Restore Database"
        cancelText="Cancel"
        danger={true}
      />
    </div>
  );
}

export default Settings;
