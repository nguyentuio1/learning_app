import { Button, Result } from 'antd'
import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <main className="page-shell">
      <Result
        status="404"
        title="404"
        subTitle="The page you requested was not found."
        extra={
          <Button type="primary">
            <Link to="/">Back home</Link>
          </Button>
        }
      />
    </main>
  )
}
