import type { SessionUser } from '../services/api';

interface TeamSelectorProps {
  users: SessionUser[];
  team1: string[];
  team2: string[];
  onToggle: (userId: string, team: 'team1' | 'team2') => void;
}

export default function TeamSelector({ users, team1, team2, onToggle }: TeamSelectorProps) {
  const activeUsers = users.filter(u => u.isActiveInSession);

  return (
    <div className="team-selection">
      <div className="team-column">
        <h4>Team 1 ({team1.length} {team1.length === 1 ? 'player' : 'players'})</h4>
        <div className="player-checkboxes">
          {activeUsers.map((user) => (
            <label 
              key={user.userId} 
              className={`player-checkbox ${team2.includes(user.userId) ? 'disabled' : ''}`}
            >
              <input
                type="checkbox"
                checked={team1.includes(user.userId)}
                onChange={() => onToggle(user.userId, 'team1')}
                disabled={team2.includes(user.userId)}
              />
              <span>{user.userName}</span>
            </label>
          ))}
        </div>
      </div>
      <div className="team-column">
        <h4>Team 2 ({team2.length} {team2.length === 1 ? 'player' : 'players'})</h4>
        <div className="player-checkboxes">
          {activeUsers.map((user) => (
            <label 
              key={user.userId} 
              className={`player-checkbox ${team1.includes(user.userId) ? 'disabled' : ''}`}
            >
              <input
                type="checkbox"
                checked={team2.includes(user.userId)}
                onChange={() => onToggle(user.userId, 'team2')}
                disabled={team1.includes(user.userId)}
              />
              <span>{user.userName}</span>
            </label>
          ))}
        </div>
      </div>
    </div>
  );
}
