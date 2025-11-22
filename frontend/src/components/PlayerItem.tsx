interface PlayerItemProps {
  userId: string;
  userName: string;
  isActive: boolean;
  totalActiveTime: string;
  sessionStatus: 'Active' | 'Completed';
  onPause: (userId: string) => void;
  onResume: (userId: string) => void;
}

function formatDuration(timeSpan: string): string {
  const parts = timeSpan.split(':');
  if (parts.length < 2) return '0m';
  
  let hours = 0;
  let minutes = 0;
  
  if (parts.length === 3) {
    const firstPart = parts[0];
    if (firstPart.includes('.')) {
      const [days, hrs] = firstPart.split('.');
      hours = parseInt(days) * 24 + parseInt(hrs);
    } else {
      hours = parseInt(firstPart);
    }
    minutes = parseInt(parts[1]);
  } else {
    minutes = parseInt(parts[0]);
  }
  
  if (hours > 0) return `${hours}h ${minutes}m`;
  return `${minutes}m`;
}

export default function PlayerItem({
  userId,
  userName,
  isActive,
  totalActiveTime,
  sessionStatus,
  onPause,
  onResume
}: PlayerItemProps) {
  return (
    <div className={`player-item ${isActive ? 'active' : 'paused'}`}>
      <div className="player-info">
        <span className="player-status-icon">
          {isActive ? '✅' : '⏸️'}
        </span>
        <span className="player-name">{userName}</span>
        <span className="player-time" title="Total active time">
          ⏱️ {formatDuration(totalActiveTime)}
        </span>
      </div>
      {sessionStatus === 'Active' && (
        <div className="player-actions">
          {isActive ? (
            <button 
              onClick={() => onPause(userId)}
              className="btn btn-warning btn-sm"
              title="Pause player"
            >
              ⏸️ Pause
            </button>
          ) : (
            <button 
              onClick={() => onResume(userId)}
              className="btn btn-success btn-sm"
              title="Resume player"
            >
              ▶️ Resume
            </button>
          )}
        </div>
      )}
    </div>
  );
}
