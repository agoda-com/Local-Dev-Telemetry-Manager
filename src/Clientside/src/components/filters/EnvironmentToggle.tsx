import { TabGroup, TabList, Tab } from '@tremor/react';

const ENVIRONMENTS = ['All', 'Local', 'CI'];

interface EnvironmentToggleProps {
  value: string;
  onChange: (value: string) => void;
}

export function EnvironmentToggle({ value, onChange }: EnvironmentToggleProps) {
  const currentIndex = ENVIRONMENTS.findIndex(
    e => e.toLowerCase() === value.toLowerCase()
  );

  return (
    <TabGroup
      index={currentIndex >= 0 ? currentIndex : 0}
      onIndexChange={(i) => onChange(ENVIRONMENTS[i].toLowerCase())}
    >
      <TabList variant="solid" className="w-fit">
        {ENVIRONMENTS.map(env => (
          <Tab key={env}>{env}</Tab>
        ))}
      </TabList>
    </TabGroup>
  );
}
