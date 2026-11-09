import { Outlet } from 'react-router'
import { NavBar } from './NavBar'

export function Layout() {
  return (
    <>
      <NavBar />
      <main className="page">
        <Outlet />
      </main>
    </>
  )
}
