import { NavLink } from 'react-router'

export function NavBar() {
  return (
    <nav className="navbar">
      <span className="navbar-title">Financial Monitor</span>
      <NavLink to="/monitor">Dashboard</NavLink>
      <NavLink to="/add">Simulator</NavLink>
    </nav>
  )
}
