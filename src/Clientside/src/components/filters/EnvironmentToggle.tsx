const ENVIRONMENTS = ['All', 'Local', 'CI'];

interface EnvironmentToggleProps {
  value: string;
  onChange: (value: string) => void;
}

export function EnvironmentToggle({ value, onChange }: EnvironmentToggleProps) {
  return (
    <div className="inline-flex rounded-xl bg-slate-100 p-1">
      {ENVIRONMENTS.map((env) => {
        const isActive = env.toLowerCase() === value.toLowerCase();
        return (
          <button
            key={env}
            onClick={() => onChange(env.toLowerCase())}
            className={`px-4 py-1.5 text-sm font-medium rounded-lg transition-all duration-150 ${
              isActive ? 'bg-white text-slate-700 shadow-sm' : 'text-slate-400 hover:text-slate-600'
            }`}
          >
            {env}
          </button>
        );
      })}
    </div>
  );
}
