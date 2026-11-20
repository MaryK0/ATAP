import jenkins.model.Jenkins

// Число параллельных обычных сборок; настройка сохраняется при перезапуске Docker.
int count = Integer.parseInt(System.getenv('JENKINS_EXECUTORS') ?: '5')
if (count < 1) throw new IllegalArgumentException('JENKINS_EXECUTORS must be positive')
Jenkins.get().setNumExecutors(count)
Jenkins.get().save()
println("ATAP: configured ${count} executors")
