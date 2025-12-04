import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import { useState } from 'react';
import Users from './pages/Users';
import Sessions from './pages/Sessions';
import SessionDetail from './pages/SessionDetail';
import Leaderboard from './pages/Leaderboard';
import Settings from './pages/Settings';
import ToastContainer from './components/ToastContainer';
import './App.css';

function App() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const closeMobileMenu = () => setMobileMenuOpen(false);

  return (
    <Router>
      <div className="app">
        <ToastContainer />
        <nav className="navbar">
          <div className="nav-container">
            <h1 className="nav-title">⚽ FIFA Tracker</h1>
            <button 
              className="hamburger-btn"
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              aria-label="Toggle menu"
            >
              {mobileMenuOpen ? '✕' : '☰'}
            </button>
            <div className={`nav-links ${mobileMenuOpen ? 'mobile-open' : ''}`}>
              <Link to="/" className="nav-link" onClick={closeMobileMenu}>Sessions</Link>
              <Link to="/users" className="nav-link" onClick={closeMobileMenu}>Users</Link>
              <Link to="/leaderboard" className="nav-link" onClick={closeMobileMenu}>Leaderboard</Link>
              <Link to="/settings" className="nav-link" onClick={closeMobileMenu}>Settings</Link>
            </div>
          </div>
        </nav>
        
        <main className="main-content">
          <Routes>
            <Route path="/" element={<Sessions />} />
            <Route path="/users" element={<Users />} />
            <Route path="/leaderboard" element={<Leaderboard />} />
            <Route path="/settings" element={<Settings />} />
            <Route path="/sessions/:id" element={<SessionDetail />} />
          </Routes>
        </main>
      </div>
    </Router>
  );
}

export default App;
