import axios from 'axios';
import { showToast } from '../components/ToastContainer';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api';

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Response interceptor for error handling
api.interceptors.response.use(
  (response) => response,
  (error) => {
    const message = error.response?.data?.message 
      || error.response?.data?.title
      || error.message 
      || 'Network error. Please check your connection.';
    
    showToast(message, 'error');
    return Promise.reject(error);
  }
);

// Types
export interface User {
  id: string;
  name: string;
  createdAt: string;
}

export interface InactiveUser {
  id: string;
  name: string;
  createdAt: string;
  deactivatedAt: string | null;
}

export interface SessionSummary {
  id: string;
  name: string;
  startDate: string;
  status: 'Active' | 'Completed';
  matchType: 'OneVsOne' | 'TwoVsTwo' | 'TwoVsOne';
  totalMatches: number;
  completedMatches: number;
  participantCount: number;
}

export interface SessionDetails {
  id: string;
  name: string;
  startDate: string;
  endDate?: string;
  status: 'Active' | 'Completed';
  matchType: 'OneVsOne' | 'TwoVsTwo' | 'TwoVsOne';
  users: SessionUser[];
  matches: Match[];
  completedMatches?: Match[]; // Only completed matches for leaderboard calculations
}

export interface SessionUser {
  userId: string;
  userName: string;
  joinedAt: string;
  isActiveInSession: boolean;
  pausedAt?: string;
  totalActiveTime: string; // TimeSpan serialized as "HH:MM:SS"
}

export interface Match {
  id: string;
  isGenerated: boolean;
  isCompleted: boolean;
  createdAt: string;
  team1Score?: number;
  team2Score?: number;
  playedAt?: string;
  team1Players: MatchPlayer[];
  team2Players: MatchPlayer[];
}

export interface MatchPlayer {
  userId: string;
  userName: string;
}

export interface LeaderboardEntry {
  userId: string;
  userName: string;
  totalMatches: number;
  wins: number;
  losses: number;
  draws: number;
  goalsScored: number;
  goalsConceded: number;
  goalDifference: number;
  winRate: number;
  points: number;
}

// Users API
export const usersApi = {
  getAll: () => api.get<User[]>('/users'),
  getInactive: () => api.get<InactiveUser[]>('/users/inactive'),
  getLeaderboard: () => api.get<LeaderboardEntry[]>('/users/leaderboard'),
  create: (name: string) => api.post<string>('/users', { Name: name }),
  update: (id: string, name: string) => api.put(`/users/${id}`, { Name: name }),
  delete: (id: string) => api.delete(`/users/${id}`),
  reactivate: (id: string) => api.post(`/users/${id}/reactivate`),
};

// Sessions API
export const sessionsApi = {
  getAll: () => api.get<SessionSummary[]>('/sessions'),
  getActive: () => api.get<SessionSummary[]>('/sessions/active'),
  getById: (id: string) => api.get<SessionDetails>(`/sessions/${id}`),
  getPendingMatches: (id: string) => api.get<Match[]>(`/sessions/${id}/pending-matches`),
  getCompletedMatches: (id: string) => api.get<Match[]>(`/sessions/${id}/completed-matches`),
  create: (name: string, matchType: string, userIds: string[]) =>
    api.post<string>('/sessions', { Name: name, MatchType: matchType, UserIds: userIds }),
  end: (id: string) => api.post(`/sessions/${id}/end`),
  addUser: (id: string, userId: string) =>
    api.post(`/sessions/${id}/users`, { UserId: userId }),
  pauseUser: (id: string, userId: string) =>
    api.post(`/sessions/${id}/users/${userId}/pause`),
  resumeUser: (id: string, userId: string) =>
    api.post(`/sessions/${id}/users/${userId}/resume`),
};

// Matches API
export const matchesApi = {
  createCustom: (sessionId: string, team1UserIds: string[], team2UserIds: string[]) =>
    api.post<string>('/matches', { SessionId: sessionId, Team1UserIds: team1UserIds, Team2UserIds: team2UserIds }),
  completeMatch: (sessionId: string, team1UserIds: string[], team2UserIds: string[], team1Score: number, team2Score: number) =>
    api.post<string>('/matches/complete', { 
      SessionId: sessionId, 
      Team1UserIds: team1UserIds, 
      Team2UserIds: team2UserIds,
      Team1Score: team1Score,
      Team2Score: team2Score
    }),
  updateScore: (id: string, team1Score: number, team2Score: number) =>
    api.put(`/matches/${id}/score`, { Team1Score: team1Score, Team2Score: team2Score }),
  delete: (id: string) => api.delete(`/matches/${id}`),
};
