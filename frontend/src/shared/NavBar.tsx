import { NavLink } from 'react-router'

export function NavBar() {
  return (
    <nav className="navbar">
      <span className="navbar-title">Financial Monitor</span>
      <NavLink to="/add">Simulator</NavLink>
    </nav>
  )
}
